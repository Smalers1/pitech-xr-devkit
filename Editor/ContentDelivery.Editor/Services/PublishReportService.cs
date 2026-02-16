#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.ContentDelivery.Editor
{
    [Serializable]
    public sealed class PublishReportWriteResult
    {
        public bool success;
        public string summary = string.Empty;
        public string assetPath = string.Empty;
        public string jsonPath = string.Empty;
    }

    public sealed class PublishReportService
    {
        private const string ReportsAssetFolder = "Assets/Settings/ContentDelivery/Reports";
        private const string DefaultWorkspaceRoot = "Build/ContentDelivery";

        public PublishTransactionReportData CreateDraft(
            AddressablesModuleConfig config,
            string source,
            string actorLabel,
            string labId,
            string labVersionId)
        {
            PublishTransactionReportData report = PublishTransactionFactory.CreateDraft(source, actorLabel);
            report.actor.editorVersion = Application.unityVersion;
            report.actor.devkitVersion = ReadDevkitVersion();
            report.actor.machineId = SystemInfo.deviceUniqueIdentifier ?? string.Empty;

            report.lab.labId = Safe(labId);
            report.lab.labVersionId = Safe(labVersionId);
            report.addressables.profileName = config != null ? Safe(config.profileName) : string.Empty;
            report.addressables.catalogMode = config != null ? config.catalogMode.ToString().ToLowerInvariant() : string.Empty;
            report.addressables.remoteLoadPathTemplate = config != null ? Safe(config.remoteCatalogUrlTemplate) : string.Empty;
            report.runtimePolicy.allowOfflineCacheLaunch = config == null || config.allowOfflineCacheLaunch;
            report.runtimePolicy.allowOlderCachedSameLab = config == null || config.allowOlderCachedSameLab;
            report.runtimePolicy.networkRequiredIfCacheMiss = config == null || config.networkRequiredIfCacheMiss;

            if (config != null)
            {
                IAddressablesConventionAdapter adapter = AddressablesAdapterResolver.Resolve(config);
                adapter.ApplyReportConventions(report);
            }

            report.idempotencyKey = PublishTransactionIdempotency.BuildKey(
                report.lab.tenantId,
                report.lab.labId,
                report.lab.labVersionId,
                "pending");
            return report;
        }

        public void ApplyValidation(
            PublishTransactionReportData report,
            AddressablesValidationResult validation,
            string actor)
        {
            if (report == null)
            {
                return;
            }

            PublishTransactionStateMachine.TryTransition(
                report,
                PublishTransactionState.Validating,
                "validation_started",
                actor);

            report.checks.Clear();
            if (validation != null && validation.checks != null)
            {
                report.checks.AddRange(validation.checks);
            }

            if (validation != null && validation.errorCount > 0)
            {
                PublishTransactionStateMachine.TryTransition(
                    report,
                    PublishTransactionState.FailedTerminal,
                    "validation_failed",
                    actor);
                report.errors.Add(new PublishErrorEntry
                {
                    code = "VALIDATION_FAILED",
                    message = validation.summary,
                    phase = "validate",
                    retryable = false,
                    terminal = true,
                });
                return;
            }

            PublishTransactionStateMachine.TryTransition(
                report,
                PublishTransactionState.Validated,
                "validation_passed",
                actor);
        }

        public void ApplyBuildStart(PublishTransactionReportData report, string actor)
        {
            if (report == null)
            {
                return;
            }

            PublishTransactionStateMachine.TryTransition(
                report,
                PublishTransactionState.BuildRequested,
                "build_requested",
                actor);
            PublishTransactionStateMachine.TryTransition(
                report,
                PublishTransactionState.Building,
                "build_started",
                actor);
        }

        public void ApplyBuildResult(
            PublishTransactionReportData report,
            AddressablesBuildResult build,
            string actor)
        {
            if (report == null || build == null)
            {
                return;
            }

            report.artifacts.buildOutputPath = Safe(build.outputPath);
            report.artifacts.catalogHash = Safe(build.catalogHash);
            report.artifacts.contentHash = Safe(build.contentHash);
            report.artifacts.bundleSizeBytes = build.bundleSizeBytes;
            report.idempotencyKey = PublishTransactionIdempotency.BuildKey(
                report.lab.tenantId,
                report.lab.labId,
                report.lab.labVersionId,
                report.artifacts.contentHash);

            if (build.success)
            {
                PublishTransactionStateMachine.TryTransition(
                    report,
                    PublishTransactionState.Built,
                    "build_succeeded",
                    actor);
                return;
            }

            PublishTransactionStateMachine.TryTransition(
                report,
                PublishTransactionState.FailedRetryable,
                "build_failed",
                actor);
            report.errors.Add(new PublishErrorEntry
            {
                code = "BUILD_FAILED",
                message = build.summary,
                phase = "build",
                retryable = true,
                terminal = false,
            });
        }

        public PublishReportWriteResult Save(
            PublishTransactionReportData report,
            AddressablesModuleConfig config = null)
        {
            PublishReportWriteResult result = new PublishReportWriteResult();
            if (report == null)
            {
                result.success = false;
                result.summary = "Cannot save null report.";
                return result;
            }

            EnsureFolder("Assets/Settings");
            EnsureFolder("Assets/Settings/ContentDelivery");
            EnsureFolder(ReportsAssetFolder);
            PruneBrokenReportAssets();

            string safeId = string.IsNullOrWhiteSpace(report.transactionId)
                ? Guid.NewGuid().ToString()
                : report.transactionId;
            string assetPath = $"{ReportsAssetFolder}/PublishTransaction_{safeId}.asset";

            string jsonFolder = ResolveReportJsonFolder(config);
            string jsonAbsoluteFolder = ToAbsolutePath(jsonFolder);
            Directory.CreateDirectory(jsonAbsoluteFolder);
            string readableBaseName = BuildReportBaseName(report);
            string jsonAbsolutePath = Path.Combine(jsonAbsoluteFolder, $"{readableBaseName}_{safeId}.json");
            string jsonPath = ToProjectRelativePath(jsonAbsolutePath);

            PublishTransactionReportAsset asset =
                AssetDatabase.LoadAssetAtPath<PublishTransactionReportAsset>(assetPath);
            if (asset == null)
            {
                // If an older/broken asset exists with missing script metadata, recreate it.
                if (File.Exists(Path.GetFullPath(assetPath)))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                    AssetDatabase.Refresh();
                }

                asset = ScriptableObject.CreateInstance<PublishTransactionReportAsset>();
                asset.Replace(report);
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            else
            {
                asset.Replace(report);
                EditorUtility.SetDirty(asset);
            }

            report.artifacts.reportJsonPath = jsonPath;
            PublishPipelineReportJson pipelineReport = BuildPipelineReportJson(report, jsonPath);
            File.WriteAllText(jsonAbsolutePath, JsonUtility.ToJson(pipelineReport, prettyPrint: true));

            AssetDatabase.SaveAssets();
            if (jsonPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceSynchronousImport);
            }

            result.success = true;
            result.assetPath = assetPath;
            result.jsonPath = jsonPath;
            result.summary = "Report asset and JSON saved.";
            return result;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!string.IsNullOrWhiteSpace(parent) && !string.IsNullOrWhiteSpace(name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void PruneBrokenReportAssets()
        {
            string reportsAbsolute = Path.GetFullPath(ReportsAssetFolder);
            if (!Directory.Exists(reportsAbsolute))
            {
                return;
            }

            string projectRoot = Path.GetFullPath(".").Replace("\\", "/");
            string[] files = Directory.GetFiles(reportsAbsolute, "PublishTransaction_*.asset", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string abs = files[i].Replace("\\", "/");
                if (!abs.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string assetPath = abs.Substring(projectRoot.Length + 1);
                PublishTransactionReportAsset typed = AssetDatabase.LoadAssetAtPath<PublishTransactionReportAsset>(assetPath);
                if (typed == null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }
        }

        private static string ResolveReportJsonFolder(AddressablesModuleConfig config)
        {
            if (config != null && !string.IsNullOrWhiteSpace(config.localReportsFolder))
            {
                return NormalizeProjectRelativePath(config.localReportsFolder, $"{DefaultWorkspaceRoot}/Reports");
            }

            if (config != null && !string.IsNullOrWhiteSpace(config.localWorkspaceRoot))
            {
                string root = NormalizeProjectRelativePath(config.localWorkspaceRoot, DefaultWorkspaceRoot);
                return $"{root}/Reports";
            }

            return $"{DefaultWorkspaceRoot}/Reports";
        }

        private static string NormalizeProjectRelativePath(string value, string fallback)
        {
            string raw = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            string normalized = raw.Replace("\\", "/").Trim('/');
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        private static string ToAbsolutePath(string relativeOrAbsolute)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
            {
                return Path.GetFullPath(".");
            }

            string normalized = relativeOrAbsolute.Replace("\\", "/");
            if (Path.IsPathRooted(normalized))
            {
                return Path.GetFullPath(normalized);
            }

            return Path.GetFullPath(Path.Combine(Path.GetFullPath("."), normalized));
        }

        private static string ToProjectRelativePath(string absolutePath)
        {
            string normalizedAbsolute = Path.GetFullPath(absolutePath).Replace("\\", "/");
            string projectRoot = Path.GetFullPath(".").Replace("\\", "/");
            if (normalizedAbsolute.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedAbsolute.Substring(projectRoot.Length + 1);
            }

            return normalizedAbsolute;
        }

        private static string ReadDevkitVersion()
        {
            string packagePath = Path.GetFullPath("Packages/pitech-xr-devkit/package.json");
            if (!File.Exists(packagePath))
            {
                packagePath = Path.GetFullPath("Packages/com.pitech.xr.devkit/package.json");
            }

            if (!File.Exists(packagePath))
            {
                return "unknown";
            }

            string json = File.ReadAllText(packagePath);
            Match match = Regex.Match(json, "\"version\"\\s*:\\s*\"(?<value>[^\"]+)\"");
            return match.Success ? match.Groups["value"].Value : "unknown";
        }

        private static string Safe(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static PublishPipelineReportJson BuildPipelineReportJson(
            PublishTransactionReportData report,
            string jsonPath)
        {
            var compact = new PublishPipelineReportJson
            {
                schemaVersion = Safe(report.schemaVersion),
                title = BuildReportTitle(report),
                transactionId = Safe(report.transactionId),
                idempotencyKey = Safe(report.idempotencyKey),
                source = Safe(report.source),
                state = Safe(report.state),
                createdAt = Safe(report.createdAt),
                updatedAt = Safe(report.updatedAt),
                lab = new PublishPipelineLabInfo
                {
                    labId = Safe(report.lab != null ? report.lab.labId : null),
                    labVersionId = Safe(report.lab != null ? report.lab.labVersionId : null),
                },
                addressables = new PublishPipelineAddressablesInfo
                {
                    groupName = Safe(report.addressables != null ? report.addressables.groupName : null),
                    profileName = Safe(report.addressables != null ? report.addressables.profileName : null),
                    buildTarget = Safe(report.addressables != null ? report.addressables.buildTarget : null),
                },
                artifacts = new PublishPipelineArtifactsInfo
                {
                    catalogHash = Safe(report.artifacts != null ? report.artifacts.catalogHash : null),
                    contentHash = Safe(report.artifacts != null ? report.artifacts.contentHash : null),
                    bundleSizeBytes = report.artifacts != null ? report.artifacts.bundleSizeBytes : 0L,
                    buildOutputPath = Safe(report.artifacts != null ? report.artifacts.buildOutputPath : null),
                    reportJsonPath = Safe(jsonPath),
                },
                runtimePolicy = new PublishRuntimePolicyInfo
                {
                    allowOfflineCacheLaunch = report.runtimePolicy == null || report.runtimePolicy.allowOfflineCacheLaunch,
                    allowOlderCachedSameLab = report.runtimePolicy == null || report.runtimePolicy.allowOlderCachedSameLab,
                    networkRequiredIfCacheMiss = report.runtimePolicy == null || report.runtimePolicy.networkRequiredIfCacheMiss,
                },
                checks = new List<PublishPipelineCheckEntry>(),
                errors = new List<PublishPipelineErrorEntry>(),
                stateHistory = new List<PublishPipelineStateEntry>(),
            };

            if (report.checks != null)
            {
                for (int i = 0; i < report.checks.Count; i++)
                {
                    PublishCheckEntry entry = report.checks[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    bool keep = !entry.passed ||
                        string.Equals(entry.severity, PublishCheckSeverity.Warning, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(entry.severity, PublishCheckSeverity.Error, StringComparison.OrdinalIgnoreCase);
                    if (!keep)
                    {
                        continue;
                    }

                    compact.checks.Add(new PublishPipelineCheckEntry
                    {
                        code = Safe(entry.code),
                        severity = Safe(entry.severity),
                        message = Safe(entry.message),
                    });
                }
            }

            if (report.errors != null)
            {
                for (int i = 0; i < report.errors.Count; i++)
                {
                    PublishErrorEntry error = report.errors[i];
                    if (error == null)
                    {
                        continue;
                    }

                    compact.errors.Add(new PublishPipelineErrorEntry
                    {
                        code = Safe(error.code),
                        phase = Safe(error.phase),
                        message = Safe(error.message),
                        retryable = error.retryable,
                        terminal = error.terminal,
                    });
                }
            }

            if (report.stateHistory != null)
            {
                for (int i = 0; i < report.stateHistory.Count; i++)
                {
                    PublishStateHistoryEntry state = report.stateHistory[i];
                    if (state == null)
                    {
                        continue;
                    }

                    compact.stateHistory.Add(new PublishPipelineStateEntry
                    {
                        toState = Safe(state.toState),
                        at = Safe(state.at),
                        reason = Safe(state.reason),
                    });
                }
            }

            return compact;
        }

        private static string BuildReportTitle(PublishTransactionReportData report)
        {
            string labId = Safe(report != null && report.lab != null ? report.lab.labId : null);
            string version = Safe(report != null && report.lab != null ? report.lab.labVersionId : null);
            string state = Safe(report != null ? report.state : null);
            if (string.IsNullOrWhiteSpace(labId))
            {
                labId = "unknown-lab";
            }
            if (string.IsNullOrWhiteSpace(version))
            {
                version = "unversioned";
            }
            if (string.IsNullOrWhiteSpace(state))
            {
                state = "unknown-state";
            }

            return $"Publish Report - {labId} - {version} - {state}";
        }

        private static string BuildReportBaseName(PublishTransactionReportData report)
        {
            string labId = NormalizeFilePart(report != null && report.lab != null ? report.lab.labId : null, "lab");
            string version = NormalizeFilePart(report != null && report.lab != null ? report.lab.labVersionId : null, "unversioned");
            string state = NormalizeFilePart(report != null ? report.state : null, "state");
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            return $"PublishReport_{labId}_{version}_{state}_{stamp}";
        }

        private static string NormalizeFilePart(string value, string fallback)
        {
            string safe = Safe(value);
            if (string.IsNullOrWhiteSpace(safe))
            {
                return fallback;
            }

            string normalized = Regex.Replace(safe, "[^a-zA-Z0-9-_]+", "-").Trim('-');
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        [Serializable]
        private sealed class PublishPipelineReportJson
        {
            public string schemaVersion = string.Empty;
            public string title = string.Empty;
            public string transactionId = string.Empty;
            public string idempotencyKey = string.Empty;
            public string source = string.Empty;
            public string state = string.Empty;
            public string createdAt = string.Empty;
            public string updatedAt = string.Empty;
            public PublishPipelineLabInfo lab = new PublishPipelineLabInfo();
            public PublishPipelineAddressablesInfo addressables = new PublishPipelineAddressablesInfo();
            public PublishPipelineArtifactsInfo artifacts = new PublishPipelineArtifactsInfo();
            public PublishRuntimePolicyInfo runtimePolicy = new PublishRuntimePolicyInfo();
            public List<PublishPipelineCheckEntry> checks = new List<PublishPipelineCheckEntry>();
            public List<PublishPipelineErrorEntry> errors = new List<PublishPipelineErrorEntry>();
            public List<PublishPipelineStateEntry> stateHistory = new List<PublishPipelineStateEntry>();
        }

        [Serializable]
        private sealed class PublishPipelineLabInfo
        {
            public string labId = string.Empty;
            public string labVersionId = string.Empty;
        }

        [Serializable]
        private sealed class PublishPipelineAddressablesInfo
        {
            public string groupName = string.Empty;
            public string profileName = string.Empty;
            public string buildTarget = string.Empty;
        }

        [Serializable]
        private sealed class PublishPipelineArtifactsInfo
        {
            public string catalogHash = string.Empty;
            public string contentHash = string.Empty;
            public long bundleSizeBytes;
            public string buildOutputPath = string.Empty;
            public string reportJsonPath = string.Empty;
        }

        [Serializable]
        private sealed class PublishPipelineCheckEntry
        {
            public string code = string.Empty;
            public string severity = string.Empty;
            public string message = string.Empty;
        }

        [Serializable]
        private sealed class PublishPipelineErrorEntry
        {
            public string code = string.Empty;
            public string phase = string.Empty;
            public string message = string.Empty;
            public bool retryable;
            public bool terminal;
        }

        [Serializable]
        private sealed class PublishPipelineStateEntry
        {
            public string toState = string.Empty;
            public string at = string.Empty;
            public string reason = string.Empty;
        }
    }
}
#endif
