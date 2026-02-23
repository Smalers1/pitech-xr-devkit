using System;
using System.Collections;
using System.Reflection;
using Pitech.XR.Core;
using UnityEngine;

#if PITECH_ADDR
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace Pitech.XR.ContentDelivery
{
    public interface IContentDeliveryMetadataProvider
    {
        bool TryResolveLabMetadata(string labId, out string resolvedVersionId, out string runtimeCatalogUrl);
    }

    public enum ContentSourceMode
    {
        LocalOnly = 0,
        OnlineOnly = 1,
        AutoOnlineWithLocalFallback = 2,
    }

    public enum OnlineMetadataSource
    {
        InternalUnity = 0,
        ExternalLaunchContext = 1,
    }

    [AddComponentMenu("Pi tech XR/Content Delivery/Content Delivery Spawner")]
    public sealed class ContentDeliverySpawner : MonoBehaviour
    {
        [Header("Content Source")]
        [Tooltip("Select how content is resolved: local only, online only, or online with local fallback.")]
        public ContentSourceMode contentSource = ContentSourceMode.AutoOnlineWithLocalFallback;

        [Tooltip("When Online/Auto is selected, choose where runtime metadata comes from.")]
        public OnlineMetadataSource onlineMetadataSource = OnlineMetadataSource.ExternalLaunchContext;

        [Tooltip("Optional Unity-side metadata provider (for Direct Supabase in Unity).")]
        public MonoBehaviour internalMetadataProvider;

        [Tooltip("If Unity provider cannot resolve metadata, fallback to manual inspector fields.")]
        public bool fallbackToManualInternalMetadata = true;

        [Header("Lab Content")]
        [Tooltip("Stable lab key used for naming and addressing conventions.")]
        public string labId = "default";

        [Tooltip("Addressables key for the lab prefab.")]
        public string addressKey = string.Empty;

        [Tooltip("Version id resolved by control plane (optional for Unity-only mode).")]
        public string resolvedVersionId = string.Empty;

        [Tooltip("Manual runtime catalog URL (used by Internal metadata source if provider is missing).")]
        public string runtimeCatalogUrl = string.Empty;

        [Tooltip("Authoring prefab. Used as fallback when addressables are unavailable.")]
        public GameObject prefabAsset;

        [Header("Spawn")]
        [Tooltip("Parent transform for spawned content. If null, this object is used.")]
        public Transform spawnParent;

        [Tooltip("Spawn as soon as scene starts.")]
        public bool loadOnSceneStart = true;

        [Tooltip("If enabled, remove existing children under spawn parent before spawning.")]
        public bool replaceExistingChildren = true;

        [Header("Runtime UX (optional)")]
        [Tooltip("Shows runtime dialogs for checking, update prompt, download progress, and errors.")]
        public bool showRuntimeDialogs = true;

        [Tooltip("Optional status overlay presenter. Assign your own UI-bound overlay here.")]
        public ContentDeliveryStatusOverlay statusOverlay;

        [Tooltip("If enabled and no overlay reference exists, a minimal overlay object is created automatically.")]
        public bool autoCreateOverlayIfMissing;

        [Tooltip("Ask learner before downloading when uncached content is detected.")]
        public bool promptBeforeDownload = true;

        [Tooltip("How many retries to allow before fallback/cancel.")]
        [Min(0)] public int maxRetryAttempts = 1;

        [Tooltip("Hide the overlay automatically after successful spawn.")]
        public bool hideStatusOverlayAfterSpawn = true;

        [Header("Analytics (optional)")]
        [Tooltip("Optional runtime analytics adapter for batched telemetry payloads.")]
        public RuntimeTelemetryAdapter analyticsAdapter;

        [Header("SceneManager Coordination (optional)")]
        [Tooltip("Optional SceneManager reference. If set, autoStart can be deferred until spawn completes.")]
        public MonoBehaviour sceneManager;

        [Tooltip("Disable SceneManager autoStart until content has spawned.")]
        public bool deferSceneManagerUntilSpawn = true;

        [Tooltip("Call SceneManager.Restart() after content is spawned.")]
        public bool restartSceneManagerAfterSpawn = true;

        private GameObject spawnedInstance;
        private bool isSpawning;

#if PITECH_ADDR
        private string loadedCatalogUrl = string.Empty;
        private AsyncOperationHandle<IResourceLocator> loadedCatalogHandle;
        private bool hasLoadedCatalogHandle;
#endif

        private void Awake()
        {
            if (spawnParent == null)
            {
                spawnParent = transform;
            }

            if (sceneManager == null)
            {
                sceneManager = FindSceneManagerLike();
            }

            if (analyticsAdapter == null)
            {
                analyticsAdapter = GetComponentInChildren<RuntimeTelemetryAdapter>(true);
            }

            if (deferSceneManagerUntilSpawn && sceneManager != null)
            {
                TrySetAutoStart(sceneManager, false);
            }
        }

        private void OnDestroy()
        {
#if PITECH_ADDR
            if (hasLoadedCatalogHandle && loadedCatalogHandle.IsValid())
            {
                Addressables.Release(loadedCatalogHandle);
            }
            hasLoadedCatalogHandle = false;
            loadedCatalogUrl = string.Empty;
#endif
        }

        private void Start()
        {
            if (loadOnSceneStart)
            {
                SpawnNow();
            }
        }

        [ContextMenu("Spawn Now")]
        public void SpawnNow()
        {
            if (isSpawning)
            {
                return;
            }

            StartCoroutine(SpawnRoutine());
        }

        public void SetManualOnlineMetadata(string versionId, string catalogUrl)
        {
            resolvedVersionId = FirstNonEmpty(versionId);
            runtimeCatalogUrl = FirstNonEmpty(catalogUrl);
        }

        [ContextMenu("Clear Spawned Content")]
        public void ClearSpawnedContent()
        {
            if (spawnedInstance != null)
            {
                Destroy(spawnedInstance);
                spawnedInstance = null;
            }

            if (replaceExistingChildren && spawnParent != null)
            {
                ClearChildren(spawnParent);
            }
        }

        private IEnumerator SpawnRoutine()
        {
            isSpawning = true;

            ContentDeliveryStatusOverlay overlay = ResolveOverlay();
            if (overlay != null)
            {
                overlay.Hide();
            }
            ContentDeliveryStatusOverlay.RuntimeUiOverride runtimeUiOverride =
                overlay != null ? overlay.runtimeUiOverride : null;

            if (replaceExistingChildren && spawnParent != null)
            {
                ClearChildren(spawnParent);
            }

            LaunchContext context = null;
            if (XRServices.TryGet<IContentDeliveryService>(out IContentDeliveryService service))
            {
                for (int i = 0; i < 30; i++)
                {
                    if (service.TryGetCurrentContext(out context) && context != null)
                    {
                        break;
                    }

                    yield return null;
                }
            }

            string effectiveLabId = FirstNonEmpty(context != null ? context.labId : null, labId, "default");
            string effectiveAddressKey = ResolveEffectiveAddressKey(context);
            bool hasInternalMetadata = TryResolveInternalMetadata(
                effectiveLabId,
                out string internalResolvedVersionId,
                out string internalCatalogUrl);
            string effectiveVersionId = ResolveEffectiveResolvedVersionId(
                context,
                hasInternalMetadata,
                internalResolvedVersionId);
            string effectiveCatalogUrl = ResolveEffectiveCatalogUrl(
                context,
                hasInternalMetadata,
                internalCatalogUrl);
            bool onlineEnabled = contentSource != ContentSourceMode.LocalOnly;
            bool onlineRequired = contentSource == ContentSourceMode.OnlineOnly;
            bool externalLaunchDriven = onlineEnabled && onlineMetadataSource == OnlineMetadataSource.ExternalLaunchContext;

            if (externalLaunchDriven)
            {
                string launchValidationError = ValidateExternalLaunchMetadata(context, effectiveVersionId, effectiveCatalogUrl);
                if (!string.IsNullOrWhiteSpace(launchValidationError))
                {
                    analyticsAdapter?.TrackError("launch_metadata_invalid", launchValidationError, true);
                    if (overlay != null)
                    {
                        overlay.ShowStatus(
                            ResolveText(runtimeUiOverride != null ? runtimeUiOverride.issueTitle : null, "Launch Blocked"),
                            launchValidationError);
                    }

                    isSpawning = false;
                    yield break;
                }
            }

            bool allowOfflineCache = context == null || context.allowOfflineCacheLaunch;
            bool allowOlderCached = context == null || context.allowOlderCachedSameLab;
            bool networkRequiredIfCacheMiss = context != null && context.networkRequiredIfCacheMiss;
            bool canUseCached = allowOfflineCache || allowOlderCached;
            bool canFallbackToLocal = prefabAsset != null && !onlineRequired && !externalLaunchDriven;

            GameObject prefabToSpawn = null;
            bool cancelled = false;
            int retriesRemaining = Mathf.Max(0, maxRetryAttempts);

            while (prefabToSpawn == null && !cancelled)
            {
                string failureMessage = null;

#if PITECH_ADDR
                if (!string.IsNullOrWhiteSpace(effectiveAddressKey))
                {
                    bool hasOnlineCatalogUrl = !string.IsNullOrWhiteSpace(effectiveCatalogUrl);
                    bool networkReachable = IsNetworkReachable();
                    bool shouldRunOnlineChecks = onlineEnabled && hasOnlineCatalogUrl && networkReachable;
                    if (onlineEnabled && onlineRequired && !hasOnlineCatalogUrl)
                    {
                        failureMessage = ResolveText(
                            runtimeUiOverride != null ? runtimeUiOverride.remoteCatalogErrorTemplate : null,
                            "Online launch metadata is missing runtime catalog URL.");
                    }
                    else if (onlineEnabled && hasOnlineCatalogUrl && !networkReachable && overlay != null)
                    {
                        overlay.ShowStatus(
                            ResolveText(runtimeUiOverride != null ? runtimeUiOverride.checkingTitle : null, "Checking Content"),
                            "Offline detected. Trying cached content...");
                    }

                    if (failureMessage == null && shouldRunOnlineChecks)
                    {
                        bool catalogOk = false;
                        string catalogError = null;
                        yield return EnsureCatalogLoaded(effectiveCatalogUrl, overlay, runtimeUiOverride, (ok, error) =>
                        {
                            catalogOk = ok;
                            catalogError = error;
                        });

                        if (!catalogOk)
                        {
                            failureMessage = FormatRuntimeTemplate(
                                runtimeUiOverride != null ? runtimeUiOverride.remoteCatalogErrorTemplate : null,
                                "Could not load remote catalog.\n{error}",
                                ("error", catalogError));
                        }
                    }

                    long bytesToDownload = 0;
                    if (failureMessage == null && shouldRunOnlineChecks)
                    {
                        bool sizeOk = false;
                        string sizeError = null;
                        yield return GetDownloadSize(effectiveAddressKey, (ok, bytes, error) =>
                        {
                            sizeOk = ok;
                            bytesToDownload = bytes;
                            sizeError = error;
                        });

                        if (!sizeOk)
                        {
                            failureMessage = FormatRuntimeTemplate(
                                runtimeUiOverride != null ? runtimeUiOverride.updateCheckErrorTemplate : null,
                                "Could not check content updates.\n{error}",
                                ("error", sizeError));
                        }
                        else
                        {
                            bool shouldDownload = bytesToDownload > 0;
                            if (bytesToDownload > 0 && !IsNetworkReachable())
                            {
                                if (networkRequiredIfCacheMiss && !canUseCached && !canFallbackToLocal)
                                {
                                    failureMessage = ResolveText(
                                        runtimeUiOverride != null ? runtimeUiOverride.networkRequiredMessage : null,
                                        "Network connection is required to download this lab content.");
                                }
                                else
                                {
                                    shouldDownload = false;
                                }
                            }

                            if (failureMessage == null && shouldDownload && promptBeforeDownload && overlay != null)
                            {
                                int updateChoice = 0;
                                string secondary = canUseCached
                                    ? ResolveText(runtimeUiOverride != null ? runtimeUiOverride.useCachedButton : null, "Use Cached")
                                    : (canFallbackToLocal
                                        ? ResolveText(runtimeUiOverride != null ? runtimeUiOverride.useLocalButton : null, "Use Local")
                                        : ResolveText(runtimeUiOverride != null ? runtimeUiOverride.cancelButton : null, "Cancel"));
                                yield return overlay.WaitForChoice(
                                    ResolveText(runtimeUiOverride != null ? runtimeUiOverride.newContentTitle : null, "New Content Found"),
                                    BuildUpdateMessage(effectiveLabId, effectiveVersionId, bytesToDownload, runtimeUiOverride),
                                    ResolveText(runtimeUiOverride != null ? runtimeUiOverride.downloadButton : null, "Download"),
                                    secondary,
                                    choice => updateChoice = choice);

                                if (updateChoice != 1)
                                {
                                    shouldDownload = false;
                                    if (!canUseCached && canFallbackToLocal)
                                    {
                                        prefabToSpawn = prefabAsset;
                                        break;
                                    }

                                    if (!canUseCached && !canFallbackToLocal)
                                    {
                                        cancelled = true;
                                        break;
                                    }
                                }
                            }

                            if (!cancelled && failureMessage == null && shouldDownload)
                            {
                                bool downloadOk = false;
                                string downloadError = null;
                                yield return DownloadDependencies(effectiveAddressKey, overlay, runtimeUiOverride, (ok, error) =>
                                {
                                    downloadOk = ok;
                                    downloadError = error;
                                });

                                if (!downloadOk)
                                {
                                    failureMessage = FormatRuntimeTemplate(
                                        runtimeUiOverride != null ? runtimeUiOverride.downloadFailedErrorTemplate : null,
                                        "Download failed.\n{error}",
                                        ("error", downloadError));
                                }
                            }
                        }
                    }

                    if (!cancelled && failureMessage == null)
                    {
                        if (overlay != null)
                        {
                            overlay.ShowStatus(
                                ResolveText(runtimeUiOverride != null ? runtimeUiOverride.loadingTitle : null, "Loading Lab"),
                                ResolveText(runtimeUiOverride != null ? runtimeUiOverride.loadingMessage : null, "Preparing immersive content..."));
                        }

                        bool loadOk = false;
                        string loadError = null;
                        GameObject loadedPrefab = null;
                        yield return LoadAddressablePrefab(effectiveAddressKey, (ok, prefab, error) =>
                        {
                            loadOk = ok;
                            loadedPrefab = prefab;
                            loadError = error;
                        });

                        if (loadOk && loadedPrefab != null)
                        {
                            prefabToSpawn = loadedPrefab;
                        }
                        else
                        {
                            failureMessage = FormatRuntimeTemplate(
                                runtimeUiOverride != null ? runtimeUiOverride.loadFailedErrorTemplate : null,
                                "Could not load lab content key '{addressKey}'.\n{error}",
                                ("addressKey", effectiveAddressKey),
                                ("error", loadError));
                        }
                    }
                }
#endif

                if (!cancelled && prefabToSpawn == null && string.IsNullOrWhiteSpace(effectiveAddressKey))
                {
                    if (canFallbackToLocal)
                    {
                        prefabToSpawn = prefabAsset;
                    }
                    else
                    {
                        failureMessage = ResolveText(
                            runtimeUiOverride != null ? runtimeUiOverride.launchFailedMessage : null,
                            onlineMetadataSource == OnlineMetadataSource.ExternalLaunchContext
                                ? "LaunchContext.addressKey is required for online launch."
                                : "Address key is required for online launch.");
                    }
                }

                if (!cancelled && prefabToSpawn == null && failureMessage == null)
                {
                    if (canFallbackToLocal)
                    {
                        prefabToSpawn = prefabAsset;
                    }
                    else
                    {
                        failureMessage = ResolveText(
                            runtimeUiOverride != null ? runtimeUiOverride.launchFailedMessage : null,
                            "No online content could be loaded for this lab.");
                    }
                }

                if (!cancelled && prefabToSpawn == null && !string.IsNullOrWhiteSpace(failureMessage))
                {
                    bool retried = false;
                    int choice = 1;

                    if (retriesRemaining > 0)
                    {
                        if (overlay != null)
                        {
                            string secondary = canFallbackToLocal
                                ? ResolveText(runtimeUiOverride != null ? runtimeUiOverride.useLocalButton : null, "Use Local")
                                : ResolveText(runtimeUiOverride != null ? runtimeUiOverride.cancelButton : null, "Cancel");
                            yield return overlay.WaitForChoice(
                                ResolveText(runtimeUiOverride != null ? runtimeUiOverride.issueTitle : null, "Content Delivery Issue"),
                                failureMessage,
                                ResolveText(runtimeUiOverride != null ? runtimeUiOverride.retryButton : null, "Retry"),
                                secondary,
                                c => choice = c);
                        }

                        if (choice == 1)
                        {
                            retriesRemaining--;
                            retried = true;
                        }
                        else if (choice == 2 && canFallbackToLocal)
                        {
                            prefabToSpawn = prefabAsset;
                        }
                        else
                        {
                            cancelled = true;
                        }
                    }

                    if (retried)
                    {
                        continue;
                    }

                    if (prefabToSpawn == null && !cancelled)
                    {
                        if (canFallbackToLocal)
                        {
                            prefabToSpawn = prefabAsset;
                        }
                        else
                        {
                            cancelled = true;
                        }
                    }
                }

                break;
            }

            if (cancelled)
            {
                if (overlay != null)
                {
                    overlay.Hide();
                }

                isSpawning = false;
                yield break;
            }

            if (prefabToSpawn == null)
            {
                Debug.LogWarning("[ContentDelivery] No prefab was available to spawn.", this);
                if (overlay != null)
                {
                    overlay.ShowStatus(
                        ResolveText(runtimeUiOverride != null ? runtimeUiOverride.launchFailedTitle : null, "Launch Failed"),
                        ResolveText(runtimeUiOverride != null ? runtimeUiOverride.launchFailedMessage : null, "No content is available for this lab."));
                    if (hideStatusOverlayAfterSpawn)
                    {
                        yield return new WaitForSecondsRealtime(1.5f);
                        overlay.Hide();
                    }
                }

                isSpawning = false;
                yield break;
            }

            Transform parent = spawnParent != null ? spawnParent : transform;
            spawnedInstance = Instantiate(prefabToSpawn, parent);
            spawnedInstance.transform.localPosition = Vector3.zero;
            spawnedInstance.transform.localRotation = Quaternion.identity;

            if (overlay != null)
            {
                overlay.ShowStatus(
                    ResolveText(runtimeUiOverride != null ? runtimeUiOverride.contentReadyTitle : null, "Content Ready"),
                    ResolveText(runtimeUiOverride != null ? runtimeUiOverride.contentReadyMessage : null, "Launching experience..."));
                if (hideStatusOverlayAfterSpawn)
                {
                    yield return new WaitForSecondsRealtime(0.35f);
                    overlay.Hide();
                }
            }

            if (sceneManager != null && restartSceneManagerAfterSpawn)
            {
                TryRestart(sceneManager);
            }

            isSpawning = false;
        }

        private ContentDeliveryStatusOverlay ResolveOverlay()
        {
            if (!showRuntimeDialogs)
            {
                return null;
            }

            if (statusOverlay != null)
            {
                return statusOverlay;
            }

            statusOverlay = GetComponentInChildren<ContentDeliveryStatusOverlay>(true);
            if (statusOverlay != null)
            {
                return statusOverlay;
            }

            ContentDeliveryStatusOverlay[] inScene = FindObjectsOfType<ContentDeliveryStatusOverlay>(true);
            if (inScene != null && inScene.Length > 0)
            {
                statusOverlay = inScene[0];
                return statusOverlay;
            }

            if (!autoCreateOverlayIfMissing)
            {
                return null;
            }

            GameObject overlayObject = new GameObject("Content Delivery Overlay");
            overlayObject.transform.SetParent(transform, false);
            statusOverlay = overlayObject.AddComponent<ContentDeliveryStatusOverlay>();
            return statusOverlay;
        }

        private static string ValidateExternalLaunchMetadata(
            LaunchContext context,
            string effectiveVersionId,
            string effectiveCatalogUrl)
        {
            if (string.IsNullOrWhiteSpace(effectiveVersionId))
            {
                return "LaunchContext.versioning.resolvedVersionId is required for online launches.";
            }

            bool launchedFromCache = context != null && context.launchedFromCache;
            if (!launchedFromCache && string.IsNullOrWhiteSpace(effectiveCatalogUrl))
            {
                return "LaunchContext.delivery.runtimeUrl is required for online launches.";
            }

            return null;
        }

        private string ResolveEffectiveAddressKey(LaunchContext context)
        {
            if (contentSource != ContentSourceMode.LocalOnly &&
                onlineMetadataSource == OnlineMetadataSource.ExternalLaunchContext)
            {
                return FirstNonEmpty(context != null ? context.addressKey : null, addressKey);
            }

            return FirstNonEmpty(addressKey);
        }

        private string ResolveEffectiveResolvedVersionId(
            LaunchContext context,
            bool hasInternalMetadata,
            string internalResolvedVersionId)
        {
            if (contentSource == ContentSourceMode.LocalOnly)
            {
                return FirstNonEmpty(resolvedVersionId, context != null ? context.resolvedVersionId : null);
            }

            if (onlineMetadataSource == OnlineMetadataSource.ExternalLaunchContext)
            {
                return FirstNonEmpty(context != null ? context.resolvedVersionId : null);
            }

            if (hasInternalMetadata)
            {
                return FirstNonEmpty(internalResolvedVersionId);
            }

            if (fallbackToManualInternalMetadata)
            {
                return FirstNonEmpty(resolvedVersionId);
            }

            return string.Empty;
        }

        private string ResolveEffectiveCatalogUrl(
            LaunchContext context,
            bool hasInternalMetadata,
            string internalCatalogUrl)
        {
            if (contentSource == ContentSourceMode.LocalOnly)
            {
                return string.Empty;
            }

            if (onlineMetadataSource == OnlineMetadataSource.ExternalLaunchContext)
            {
                return FirstNonEmpty(context != null ? context.runtimeUrl : null);
            }

            if (hasInternalMetadata)
            {
                return FirstNonEmpty(internalCatalogUrl);
            }

            if (fallbackToManualInternalMetadata)
            {
                return FirstNonEmpty(runtimeCatalogUrl);
            }

            return string.Empty;
        }

        private bool TryResolveInternalMetadata(
            string requestedLabId,
            out string providerResolvedVersionId,
            out string providerRuntimeCatalogUrl)
        {
            providerResolvedVersionId = string.Empty;
            providerRuntimeCatalogUrl = string.Empty;
            if (onlineMetadataSource != OnlineMetadataSource.InternalUnity)
            {
                return false;
            }

            if (internalMetadataProvider is IContentDeliveryMetadataProvider directProvider)
            {
                if (directProvider.TryResolveLabMetadata(
                    FirstNonEmpty(requestedLabId, labId, "default"),
                    out providerResolvedVersionId,
                    out providerRuntimeCatalogUrl))
                {
                    providerResolvedVersionId = FirstNonEmpty(providerResolvedVersionId);
                    providerRuntimeCatalogUrl = FirstNonEmpty(providerRuntimeCatalogUrl);
                    return true;
                }
            }

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null || behaviours[i] == this || behaviours[i] == internalMetadataProvider)
                {
                    continue;
                }

                if (behaviours[i] is IContentDeliveryMetadataProvider siblingProvider &&
                    siblingProvider.TryResolveLabMetadata(
                        FirstNonEmpty(requestedLabId, labId, "default"),
                        out providerResolvedVersionId,
                        out providerRuntimeCatalogUrl))
                {
                    providerResolvedVersionId = FirstNonEmpty(providerResolvedVersionId);
                    providerRuntimeCatalogUrl = FirstNonEmpty(providerRuntimeCatalogUrl);
                    return true;
                }
            }

            return false;
        }

        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private static bool IsNetworkReachable()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }

        private string BuildUpdateMessage(
            string lab,
            string version,
            long bytes,
            ContentDeliveryStatusOverlay.RuntimeUiOverride runtimeUiOverride)
        {
            string normalizedLab = string.IsNullOrWhiteSpace(lab) ? "this lab" : lab.Trim();
            string sizeText = FormatBytes(bytes);
            if (!string.IsNullOrWhiteSpace(version) && !string.Equals(version, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                return FormatRuntimeTemplate(
                    runtimeUiOverride != null ? runtimeUiOverride.updateAvailableWithVersionMessageTemplate : null,
                    "New content is available for {labId} (version {resolvedVersionId}).\nDownload {size} now?",
                    ("labId", normalizedLab),
                    ("resolvedVersionId", version.Trim()),
                    ("size", sizeText));
            }

            return FormatRuntimeTemplate(
                runtimeUiOverride != null ? runtimeUiOverride.updateAvailableMessageTemplate : null,
                "New content is available for {labId}.\nDownload {size} now?",
                ("labId", normalizedLab),
                ("size", sizeText));
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 B";
            }

            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024d && unit < units.Length - 1)
            {
                value /= 1024d;
                unit++;
            }

            return $"{value:0.##} {units[unit]}";
        }

        private static string ResolveText(string candidate, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }

            return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback;
        }

        private static string FormatRuntimeTemplate(
            string candidateTemplate,
            string fallbackTemplate,
            params (string key, string value)[] values)
        {
            string result = ResolveText(candidateTemplate, fallbackTemplate);
            if (values == null || values.Length == 0)
            {
                return result;
            }

            for (int i = 0; i < values.Length; i++)
            {
                string key = values[i].key;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                string token = "{" + key + "}";
                string replacement = values[i].value ?? string.Empty;
                result = result.Replace(token, replacement);
            }

            return result;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

#if PITECH_ADDR
        private IEnumerator EnsureCatalogLoaded(
            string catalogUrl,
            ContentDeliveryStatusOverlay overlay,
            ContentDeliveryStatusOverlay.RuntimeUiOverride runtimeUiOverride,
            Action<bool, string> onComplete)
        {
            string targetUrl = string.IsNullOrWhiteSpace(catalogUrl) ? string.Empty : catalogUrl.Trim();
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                onComplete?.Invoke(true, null);
                yield break;
            }

            if (hasLoadedCatalogHandle &&
                string.Equals(targetUrl, loadedCatalogUrl, StringComparison.OrdinalIgnoreCase) &&
                loadedCatalogHandle.IsValid() &&
                loadedCatalogHandle.Status == AsyncOperationStatus.Succeeded)
            {
                onComplete?.Invoke(true, null);
                yield break;
            }

            if (overlay != null)
            {
                overlay.ShowStatus(
                    ResolveText(runtimeUiOverride != null ? runtimeUiOverride.checkingTitle : null, "Checking Content"),
                    ResolveText(runtimeUiOverride != null ? runtimeUiOverride.checkingMessage : null, "Resolving remote catalog..."));
            }

            AsyncOperationHandle<IResourceLocator> previousHandle = loadedCatalogHandle;
            string previousUrl = loadedCatalogUrl;
            bool hadPreviousCatalog = hasLoadedCatalogHandle && previousHandle.IsValid();
            AsyncOperationHandle<IResourceLocator> catalogHandle = Addressables.LoadContentCatalogAsync(targetUrl, false);
            yield return catalogHandle;

            if (catalogHandle.Status == AsyncOperationStatus.Succeeded)
            {
                if (hadPreviousCatalog)
                {
                    Addressables.Release(previousHandle);
                }
                loadedCatalogHandle = catalogHandle;
                loadedCatalogUrl = targetUrl;
                hasLoadedCatalogHandle = true;
                onComplete?.Invoke(true, null);
            }
            else
            {
                string error = catalogHandle.OperationException != null
                    ? catalogHandle.OperationException.Message
                    : $"Catalog load status: {catalogHandle.Status}";
                if (catalogHandle.IsValid())
                {
                    Addressables.Release(catalogHandle);
                }
                loadedCatalogHandle = previousHandle;
                loadedCatalogUrl = previousUrl;
                hasLoadedCatalogHandle = hadPreviousCatalog;
                onComplete?.Invoke(false, error);
            }
        }

        private IEnumerator GetDownloadSize(string key, Action<bool, long, string> onComplete)
        {
            AsyncOperationHandle<long> sizeHandle = Addressables.GetDownloadSizeAsync(key);
            yield return sizeHandle;

            if (sizeHandle.Status == AsyncOperationStatus.Succeeded)
            {
                long bytes = sizeHandle.Result;
                if (sizeHandle.IsValid())
                {
                    Addressables.Release(sizeHandle);
                }
                onComplete?.Invoke(true, bytes, null);
                yield break;
            }

            string error = sizeHandle.OperationException != null
                ? sizeHandle.OperationException.Message
                : $"Size check status: {sizeHandle.Status}";
            if (sizeHandle.IsValid())
            {
                Addressables.Release(sizeHandle);
            }
            onComplete?.Invoke(false, 0L, error);
        }

        private IEnumerator DownloadDependencies(
            string key,
            ContentDeliveryStatusOverlay overlay,
            ContentDeliveryStatusOverlay.RuntimeUiOverride runtimeUiOverride,
            Action<bool, string> onComplete)
        {
            AsyncOperationHandle downloadHandle = Addressables.DownloadDependenciesAsync(key, false);
            while (!downloadHandle.IsDone)
            {
                var status = downloadHandle.GetDownloadStatus();
                if (overlay != null)
                {
                    float progress = status.TotalBytes > 0
                        ? Mathf.Clamp01((float)status.DownloadedBytes / status.TotalBytes)
                        : Mathf.Clamp01(downloadHandle.PercentComplete);
                    string progressText = status.TotalBytes > 0
                        ? $"{FormatBytes((long)status.DownloadedBytes)} / {FormatBytes((long)status.TotalBytes)}"
                        : $"{Mathf.RoundToInt(progress * 100f)}%";
                    overlay.ShowProgress(
                        ResolveText(runtimeUiOverride != null ? runtimeUiOverride.downloadingTitle : null, "Downloading Content"),
                        ResolveText(runtimeUiOverride != null ? runtimeUiOverride.downloadingMessage : null, "Downloading required lab data..."),
                        progress,
                        progressText);
                }

                analyticsAdapter?.TrackDownloadProgress((long)status.DownloadedBytes, (long)status.TotalBytes);
                yield return null;
            }

            if (analyticsAdapter != null)
            {
                var finalStatus = downloadHandle.GetDownloadStatus();
                analyticsAdapter.TrackDownloadProgress((long)finalStatus.DownloadedBytes, (long)finalStatus.TotalBytes);
            }

            bool success = downloadHandle.Status == AsyncOperationStatus.Succeeded;
            string error = success
                ? null
                : (downloadHandle.OperationException != null
                    ? downloadHandle.OperationException.Message
                    : $"Download status: {downloadHandle.Status}");
            if (downloadHandle.IsValid())
            {
                Addressables.Release(downloadHandle);
            }
            onComplete?.Invoke(success, error);
        }

        private IEnumerator LoadAddressablePrefab(string key, Action<bool, GameObject, string> onComplete)
        {
            AsyncOperationHandle<GameObject> loadHandle = Addressables.LoadAssetAsync<GameObject>(key);
            yield return loadHandle;

            if (loadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                GameObject result = loadHandle.Result;
                if (loadHandle.IsValid())
                {
                    Addressables.Release(loadHandle);
                }
                onComplete?.Invoke(result != null, result, null);
                yield break;
            }

            string error = loadHandle.OperationException != null
                ? loadHandle.OperationException.Message
                : $"Load status: {loadHandle.Status}";
            if (loadHandle.IsValid())
            {
                Addressables.Release(loadHandle);
            }
            onComplete?.Invoke(false, null, error);
        }
#endif

        private static MonoBehaviour FindSceneManagerLike()
        {
            MonoBehaviour[] all = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour behaviour = all[i];
                if (behaviour != null && behaviour.GetType().FullName == "Pitech.XR.Scenario.SceneManager")
                {
                    return behaviour;
                }
            }

            return null;
        }

        private static void TrySetAutoStart(MonoBehaviour target, bool value)
        {
            if (target == null)
            {
                return;
            }

            var type = target.GetType();
            FieldInfo field = type.GetField("autoStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo prop = type.GetProperty("autoStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(bool) && prop.CanWrite)
            {
                prop.SetValue(target, value);
            }
        }

        private static void TryRestart(MonoBehaviour target)
        {
            if (target == null)
            {
                return;
            }

            MethodInfo restart = target.GetType().GetMethod(
                "Restart",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            restart?.Invoke(target, null);
        }
    }
}


