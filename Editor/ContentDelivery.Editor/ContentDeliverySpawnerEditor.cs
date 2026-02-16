#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.ContentDelivery.Editor
{
    [CustomEditor(typeof(ContentDeliverySpawner), true)]
    public sealed class ContentDeliverySpawnerEditor : UnityEditor.Editor
    {
        private static readonly string[] ContentSourceLabels =
        {
            "Local",
            "Online (Required)",
            "Auto (Online + Cache)"
        };

        private static readonly string[] OnlineMetadataLabels =
        {
            "Internal (Unity Config - Provider)",
            "External (Launch Context)"
        };

        private SerializedProperty contentSourceProp;
        private SerializedProperty onlineMetadataSourceProp;
        private SerializedProperty internalMetadataProviderProp;
        private SerializedProperty fallbackToManualInternalMetadataProp;
        private SerializedProperty labIdProp;
        private SerializedProperty addressKeyProp;
        private SerializedProperty resolvedVersionIdProp;
        private SerializedProperty runtimeCatalogUrlProp;
        private SerializedProperty prefabAssetProp;
        private SerializedProperty spawnParentProp;
        private SerializedProperty loadOnSceneStartProp;
        private SerializedProperty replaceExistingChildrenProp;
        private SerializedProperty showRuntimeDialogsProp;
        private SerializedProperty statusOverlayProp;
        private SerializedProperty autoCreateOverlayIfMissingProp;
        private SerializedProperty promptBeforeDownloadProp;
        private SerializedProperty maxRetryAttemptsProp;
        private SerializedProperty hideStatusOverlayAfterSpawnProp;
        private SerializedProperty sceneManagerProp;
        private SerializedProperty deferSceneManagerUntilSpawnProp;
        private SerializedProperty restartSceneManagerAfterSpawnProp;

        private void OnEnable()
        {
            contentSourceProp = serializedObject.FindProperty("contentSource");
            onlineMetadataSourceProp = serializedObject.FindProperty("onlineMetadataSource");
            internalMetadataProviderProp = serializedObject.FindProperty("internalMetadataProvider");
            fallbackToManualInternalMetadataProp = serializedObject.FindProperty("fallbackToManualInternalMetadata");
            labIdProp = serializedObject.FindProperty("labId");
            addressKeyProp = serializedObject.FindProperty("addressKey");
            resolvedVersionIdProp = serializedObject.FindProperty("resolvedVersionId");
            runtimeCatalogUrlProp = serializedObject.FindProperty("runtimeCatalogUrl");
            prefabAssetProp = serializedObject.FindProperty("prefabAsset");
            spawnParentProp = serializedObject.FindProperty("spawnParent");
            loadOnSceneStartProp = serializedObject.FindProperty("loadOnSceneStart");
            replaceExistingChildrenProp = serializedObject.FindProperty("replaceExistingChildren");
            showRuntimeDialogsProp = serializedObject.FindProperty("showRuntimeDialogs");
            statusOverlayProp = serializedObject.FindProperty("statusOverlay");
            autoCreateOverlayIfMissingProp = serializedObject.FindProperty("autoCreateOverlayIfMissing");
            promptBeforeDownloadProp = serializedObject.FindProperty("promptBeforeDownload");
            maxRetryAttemptsProp = serializedObject.FindProperty("maxRetryAttempts");
            hideStatusOverlayAfterSpawnProp = serializedObject.FindProperty("hideStatusOverlayAfterSpawn");
            sceneManagerProp = serializedObject.FindProperty("sceneManager");
            deferSceneManagerUntilSpawnProp = serializedObject.FindProperty("deferSceneManagerUntilSpawn");
            restartSceneManagerAfterSpawnProp = serializedObject.FindProperty("restartSceneManagerAfterSpawn");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            int step = 1;
            DrawSourceSection(step++);
            bool showLabSection = ShouldShowLabSection();
            if (showLabSection)
            {
                DrawLabSection(step++);
            }
            DrawSpawnSection(step++);
            DrawRuntimeUxSection(step++);
            DrawSceneManagerSection(step++);
            serializedObject.ApplyModifiedProperties();

            var spawner = (ContentDeliverySpawner)target;
            DrawEasyActions(spawner);
        }

        private void DrawSourceSection(int stepNumber)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"{stepNumber}) Content Source", EditorStyles.boldLabel);
            ContentSourceMode mode = (ContentSourceMode)Mathf.Clamp(contentSourceProp.enumValueIndex, 0, ContentSourceLabels.Length - 1);
            int selectedMode = EditorGUILayout.Popup("Content Source", (int)mode, ContentSourceLabels);
            if (selectedMode != (int)mode)
            {
                contentSourceProp.enumValueIndex = selectedMode;
                mode = (ContentSourceMode)selectedMode;
            }

            bool onlineEnabled = mode != ContentSourceMode.LocalOnly;
            if (!onlineEnabled)
            {
                EditorGUILayout.HelpBox(
                    "Local mode skips online metadata and uses addressable key/local prefab only.",
                    MessageType.None);
                return;
            }

            OnlineMetadataSource metadataSource = (OnlineMetadataSource)Mathf.Clamp(
                onlineMetadataSourceProp.enumValueIndex, 0, OnlineMetadataLabels.Length - 1);
            int selectedMetadata = EditorGUILayout.Popup("Online Metadata Source", (int)metadataSource, OnlineMetadataLabels);
            if (selectedMetadata != (int)metadataSource)
            {
                onlineMetadataSourceProp.enumValueIndex = selectedMetadata;
                metadataSource = (OnlineMetadataSource)selectedMetadata;
            }

            if (metadataSource == OnlineMetadataSource.InternalUnity)
            {
                EditorGUILayout.PropertyField(internalMetadataProviderProp);
                EditorGUILayout.PropertyField(fallbackToManualInternalMetadataProp);
                EditorGUILayout.PropertyField(resolvedVersionIdProp);
                EditorGUILayout.PropertyField(runtimeCatalogUrlProp);
                EditorGUILayout.HelpBox(
                    "Internal mode: use a Unity provider (e.g. Direct Supabase) or manual runtime catalog URL.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "External mode: addressKey + runtimeUrl are required from LaunchContext. labId + resolvedVersionId are recommended metadata.",
                    MessageType.Info);
            }
        }

        private void DrawLabSection(int stepNumber)
        {
            if (!ShouldShowLabSection())
            {
                return;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"{stepNumber}) Lab Identity & Key", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(labIdProp);
            EditorGUILayout.PropertyField(addressKeyProp);

            ContentSourceMode mode = (ContentSourceMode)contentSourceProp.enumValueIndex;
            if (mode == ContentSourceMode.OnlineOnly)
            {
                EditorGUILayout.HelpBox(
                    "Prefab fallback is disabled in Online (Required) mode.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.PropertyField(prefabAssetProp);
            }
        }

        private void DrawSpawnSection(int stepNumber)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"{stepNumber}) Spawn Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(spawnParentProp);
            EditorGUILayout.PropertyField(loadOnSceneStartProp);
            EditorGUILayout.PropertyField(replaceExistingChildrenProp);
        }

        private void DrawRuntimeUxSection(int stepNumber)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"{stepNumber}) Runtime UX", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(showRuntimeDialogsProp);
            if (!showRuntimeDialogsProp.boolValue)
            {
                return;
            }

            EditorGUILayout.PropertyField(statusOverlayProp);
            EditorGUILayout.PropertyField(autoCreateOverlayIfMissingProp);
            EditorGUILayout.PropertyField(promptBeforeDownloadProp);
            EditorGUILayout.PropertyField(maxRetryAttemptsProp);
            EditorGUILayout.PropertyField(hideStatusOverlayAfterSpawnProp);
        }

        private void DrawSceneManagerSection(int stepNumber)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"{stepNumber}) SceneManager Coordination", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(sceneManagerProp);
            EditorGUILayout.PropertyField(deferSceneManagerUntilSpawnProp);
            EditorGUILayout.PropertyField(restartSceneManagerAfterSpawnProp);
        }

        private bool ShouldShowLabSection()
        {
            ContentSourceMode mode = (ContentSourceMode)contentSourceProp.enumValueIndex;
            if (mode == ContentSourceMode.LocalOnly)
            {
                return true;
            }

            OnlineMetadataSource metadataSource = (OnlineMetadataSource)onlineMetadataSourceProp.enumValueIndex;
            return metadataSource != OnlineMetadataSource.ExternalLaunchContext;
        }

        private void DrawEasyActions(ContentDeliverySpawner spawner)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Easy Actions", EditorStyles.boldLabel);
            bool externalLaunchFlow = !ShouldShowLabSection();
            EditorGUILayout.HelpBox(
                externalLaunchFlow
                    ? "Typical flow: choose source mode -> provide LaunchContext payload (required: addressKey + runtimeUrl; recommended: labId + resolvedVersionId) -> assign overlay -> play."
                    : "Typical flow: choose source mode -> set prefab -> map addressable -> assign overlay -> play.",
                MessageType.None);

            if (spawner.spawnParent == null)
            {
                if (GUILayout.Button("Create Spawn Parent"))
                {
                    var root = new GameObject("Lab Content Root");
                    Undo.RegisterCreatedObjectUndo(root, "Create Spawn Parent");
                    root.transform.SetParent(spawner.transform, false);
                    Undo.RecordObject(spawner, "Assign Spawn Parent");
                    spawner.spawnParent = root.transform;
                    EditorUtility.SetDirty(spawner);
                }
            }

            if (spawner.showRuntimeDialogs && spawner.statusOverlay == null)
            {
                var existingOverlay = FindObjectOfType<ContentDeliveryStatusOverlay>(true);
                if (existingOverlay != null && GUILayout.Button("Assign Existing Runtime Status Overlay"))
                {
                    Undo.RecordObject(spawner, "Assign Runtime Status Overlay");
                    spawner.statusOverlay = existingOverlay;
                    EditorUtility.SetDirty(spawner);
                }

                if (GUILayout.Button("Create Runtime Status Overlay"))
                {
                    var overlayObject = new GameObject("Content Delivery Overlay");
                    Undo.RegisterCreatedObjectUndo(overlayObject, "Create Runtime Status Overlay");
                    overlayObject.transform.SetParent(spawner.transform, false);
                    var overlay = overlayObject.AddComponent<ContentDeliveryStatusOverlay>();
                    Undo.RecordObject(spawner, "Assign Runtime Status Overlay");
                    spawner.statusOverlay = overlay;
                    EditorUtility.SetDirty(spawner);
                }
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Spawn Now (Play Mode)"))
                {
                    spawner.SpawnNow();
                }
            }

#if PITECH_ADDR
            if (GUILayout.Button("Use Prefab Asset as Lab Addressable"))
            {
                ConvertPrefabToAddressable(spawner);
            }
#else
            EditorGUILayout.HelpBox(
                "Addressables package is not available. Prefab will spawn from direct prefab fallback.",
                MessageType.None);
#endif
        }

#if PITECH_ADDR
        private static void ConvertPrefabToAddressable(ContentDeliverySpawner spawner)
        {
            if (spawner == null)
                return;

            if (spawner.prefabAsset == null)
            {
                EditorUtility.DisplayDialog("Content Delivery", "Assign a Prefab Asset first.", "OK");
                return;
            }

            var service = new AddressablesService();
            AddressablesModuleConfig cfg = service.EnsureConfigAsset(out _, out _);
            AddressablesMarkPrefabResult map = service.MarkPrefabAddressable(cfg, spawner.labId, spawner.prefabAsset, dryRun: false);
            if (!map.success)
            {
                EditorUtility.DisplayDialog("Content Delivery", map.summary, "OK");
                return;
            }

            Undo.RecordObject(spawner, "Assign Addressable Key");
            spawner.addressKey = map.addressKey;
            if (spawner.spawnParent == null)
                spawner.spawnParent = spawner.transform;
            EditorUtility.SetDirty(spawner);

            EditorUtility.DisplayDialog(
                "Content Delivery",
                $"Prefab was mapped as addressable.\n\nGroup: {map.groupName}\nKey: {map.addressKey}",
                "OK");
        }
#endif

    }
}
#endif
