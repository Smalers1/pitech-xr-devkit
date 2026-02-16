#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

#if PITECH_ADDR
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
#endif

namespace Pitech.XR.ContentDelivery.Editor
{
    [Serializable]
    public sealed class AddressablesValidationResult
    {
        public bool success;
        public string summary = string.Empty;
        public string profileName = string.Empty;
        public string expectedGroupName = string.Empty;
        public int errorCount;
        public int warningCount;
        public List<PublishCheckEntry> checks = new List<PublishCheckEntry>();

        public void Add(PublishCheckEntry check)
        {
            checks.Add(check);
            if (!check.passed)
            {
                if (string.Equals(check.severity, PublishCheckSeverity.Error, StringComparison.Ordinal))
                {
                    errorCount++;
                }
                else if (string.Equals(check.severity, PublishCheckSeverity.Warning, StringComparison.Ordinal))
                {
                    warningCount++;
                }
            }
        }
    }

    public sealed class AddressablesValidationService
    {
        public AddressablesValidationResult Validate(AddressablesModuleConfig config, string labIdHint)
        {
            AddressablesValidationResult result = new AddressablesValidationResult();

            if (config == null)
            {
                result.Add(Fail("CONFIG_MISSING", PublishCheckSeverity.Error, "Config asset is missing.", "config"));
                result.success = false;
                result.summary = "Validation failed: no config.";
                return result;
            }

            IAddressablesConventionAdapter adapter = AddressablesAdapterResolver.Resolve(config);
            string groupName = adapter.BuildGroupName(config, string.IsNullOrWhiteSpace(labIdHint) ? "default" : labIdHint);
            result.expectedGroupName = groupName;
            result.profileName = string.IsNullOrWhiteSpace(config.profileName) ? "Default" : config.profileName.Trim();

            if (!ContentDeliveryCapability.HasAddressablesPackage)
            {
                result.Add(Fail(
                    "ADDR_PACKAGE_MISSING",
                    PublishCheckSeverity.Error,
                    "Addressables package is not installed.",
                    "packages",
                    "Install com.unity.addressables."));
                result.success = false;
                result.summary = "Validation failed: Addressables package missing.";
                return result;
            }

#if PITECH_ADDR
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                result.Add(Fail(
                    "SETTINGS_MISSING",
                    PublishCheckSeverity.Error,
                    "Addressables settings asset not found.",
                    "settings",
                    "Run Setup first."));
                result.success = false;
                result.summary = "Validation failed: settings missing.";
                return result;
            }

            ValidateProfile(settings, config, result);
            ValidateRemoteTemplate(config, result);
            ValidateExpectedGroup(settings, groupName, result);
            ValidateRemoteSchema(settings, groupName, result);
            ValidateEmptyGroups(settings, result);
            ValidateDuplicateAddresses(settings, result);
            ValidateGroupPerLabMapping(settings, adapter, result);
            ValidateBuildTarget(result);
#else
            result.Add(Fail(
                "DEFINE_MISSING",
                PublishCheckSeverity.Error,
                "PITECH_ADDR define is unavailable.",
                "compile",
                "Reimport scripts so versionDefines can resolve package symbols."));
#endif

            result.success = result.errorCount == 0;
            result.summary = result.success
                ? $"Validation passed with {result.warningCount} warning(s)."
                : $"Validation failed with {result.errorCount} error(s) and {result.warningCount} warning(s).";
            return result;
        }

#if PITECH_ADDR
        private static void ValidateProfile(
            AddressableAssetSettings settings,
            AddressablesModuleConfig config,
            AddressablesValidationResult result)
        {
            string profileName = string.IsNullOrWhiteSpace(config.profileName) ? "Default" : config.profileName.Trim();
            string profileId = settings.profileSettings.GetProfileId(profileName);
            if (string.IsNullOrEmpty(profileId))
            {
                result.Add(Fail(
                    "PROFILE_MISSING",
                    PublishCheckSeverity.Error,
                    $"Profile '{profileName}' does not exist.",
                    "profiles",
                    "Run Setup to create required profile."));
                return;
            }

            string loadPath = settings.profileSettings.GetValueByName(profileId, AddressableAssetSettings.kRemoteLoadPath);
            string buildPath = settings.profileSettings.GetValueByName(profileId, AddressableAssetSettings.kRemoteBuildPath);
            if (string.IsNullOrWhiteSpace(loadPath))
            {
                result.Add(Fail(
                    "REMOTE_LOADPATH_EMPTY",
                    PublishCheckSeverity.Error,
                    "Remote load path is empty.",
                    "profiles",
                    "Set a valid remote URL template."));
            }
            else if (config.catalogMode == CatalogMode.Remote && !loadPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(Fail(
                    "REMOTE_LOADPATH_INVALID",
                    PublishCheckSeverity.Warning,
                    "Remote catalog mode is enabled but remote load path does not look like an HTTP URL.",
                    "profiles",
                    "Use a valid HTTP/HTTPS remote load path."));
            }
            else
            {
                result.Add(Pass("PROFILE_OK", "Profile and remote load path are configured.", "profiles"));
            }

            if (string.IsNullOrWhiteSpace(buildPath))
            {
                result.Add(Fail(
                    "REMOTE_BUILDPATH_EMPTY",
                    PublishCheckSeverity.Error,
                    "Remote build path is empty.",
                    "profiles",
                    "Set Remote.BuildPath to a valid project-relative output path."));
            }
            else
            {
                string normalizedBuildPath = buildPath.Trim().Replace("\\", "/");
                if (normalizedBuildPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalizedBuildPath, "Assets", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(Fail(
                        "REMOTE_BUILDPATH_IN_ASSETS",
                        PublishCheckSeverity.Warning,
                        "Remote build path points inside Assets. This causes unnecessary asset imports and project churn.",
                        "profiles",
                        "Prefer a project-relative folder outside Assets (for example Build/ContentDelivery/Addressables/...)."));
                }
                else
                {
                    result.Add(Pass("REMOTE_BUILDPATH_OK", "Remote build path is outside Assets (recommended).", "profiles"));
                }
            }
        }

        private static void ValidateExpectedGroup(
            AddressableAssetSettings settings,
            string groupName,
            AddressablesValidationResult result)
        {
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                result.Add(Fail(
                    "GROUP_MISSING",
                    PublishCheckSeverity.Error,
                    $"Expected remote group '{groupName}' does not exist.",
                    "groups",
                    "Run Setup to create the lab group."));
            }
            else
            {
                result.Add(Pass("GROUP_EXISTS", $"Group '{groupName}' exists.", "groups"));
            }
        }

        private static void ValidateRemoteTemplate(
            AddressablesModuleConfig config,
            AddressablesValidationResult result)
        {
            string template = config.remoteCatalogUrlTemplate ?? string.Empty;
            if (config.catalogMode == CatalogMode.Local)
            {
                result.Add(Pass("TEMPLATE_LOCAL_MODE", "Local catalog mode does not require remote template validation.", "config"));
                return;
            }

            if (string.IsNullOrWhiteSpace(template))
            {
                result.Add(Fail(
                    "REMOTE_TEMPLATE_EMPTY",
                    PublishCheckSeverity.Warning,
                    "Remote catalog URL template is empty.",
                    "config",
                    "Provide a template containing base URL and identifiers."));
                return;
            }

            bool hasEnvironment = template.IndexOf("{environment}", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasLab = template.IndexOf("{labId}", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasVersion = template.IndexOf("{resolvedVersionId}", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!hasEnvironment || !hasLab || !hasVersion)
            {
                result.Add(Fail(
                    "REMOTE_TEMPLATE_PLACEHOLDERS",
                    PublishCheckSeverity.Warning,
                    "Remote template is missing one or more recommended placeholders ({environment}, {labId}, {resolvedVersionId}).",
                    "config",
                    "Use all placeholders to keep one-group-per-lab-version routing explicit."));
            }
            else
            {
                result.Add(Pass("REMOTE_TEMPLATE_OK", "Remote catalog template includes recommended placeholders.", "config"));
            }
        }

        private static void ValidateRemoteSchema(
            AddressableAssetSettings settings,
            string groupName,
            AddressablesValidationResult result)
        {
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                return;
            }

            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled == null)
            {
                result.Add(Fail(
                    "BUNDLED_SCHEMA_MISSING",
                    PublishCheckSeverity.Error,
                    "BundledAssetGroupSchema is missing from the expected remote group.",
                    "groups",
                    "Add BundledAssetGroupSchema to the group."));
                return;
            }

            string loadPath = bundled.LoadPath.GetValue(settings);
            if (string.IsNullOrWhiteSpace(loadPath))
            {
                result.Add(Fail(
                    "GROUP_LOADPATH_EMPTY",
                    PublishCheckSeverity.Error,
                    "Group load path resolves to empty value.",
                    "groups",
                    "Set LoadPath to RemoteLoadPath variable."));
            }
            else
            {
                result.Add(Pass("GROUP_SCHEMA_OK", "Remote group schema is configured.", "groups"));
            }
        }

        private static void ValidateEmptyGroups(
            AddressableAssetSettings settings,
            AddressablesValidationResult result)
        {
            int emptyGroupCount = 0;
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null || group.ReadOnly)
                {
                    continue;
                }

                if (group.entries == null || group.entries.Count == 0)
                {
                    emptyGroupCount++;
                    result.Add(Fail(
                        "EMPTY_GROUP",
                        PublishCheckSeverity.Warning,
                        $"Group '{group.Name}' has no entries.",
                        "groups",
                        "Add assets or remove unused groups."));
                }
            }

            if (emptyGroupCount == 0)
            {
                result.Add(Pass("NO_EMPTY_GROUPS", "No empty writable groups found.", "groups"));
            }
        }

        private static void ValidateDuplicateAddresses(
            AddressableAssetSettings settings,
            AddressablesValidationResult result)
        {
            Dictionary<string, string> seen = new Dictionary<string, string>(StringComparer.Ordinal);
            int duplicates = 0;
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.address))
                    {
                        continue;
                    }

                    string address = entry.address.Trim();
                    if (seen.TryGetValue(address, out string existingGroup))
                    {
                        duplicates++;
                        result.Add(Fail(
                            "DUPLICATE_ADDRESS",
                            PublishCheckSeverity.Error,
                            $"Duplicate address '{address}' in groups '{existingGroup}' and '{group.Name}'.",
                            "entries",
                            "Ensure every addressable key is unique."));
                    }
                    else
                    {
                        seen[address] = group.Name;
                    }
                }
            }

            if (duplicates == 0)
            {
                result.Add(Pass("NO_DUPLICATES", "No duplicate address keys found.", "entries"));
            }
        }

        private static void ValidateGroupPerLabMapping(
            AddressableAssetSettings settings,
            IAddressablesConventionAdapter adapter,
            AddressablesValidationResult result)
        {
            Dictionary<string, string> labToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                if (!adapter.TryParseLabId(group.Name, out string labId))
                {
                    continue;
                }

                if (labToGroup.TryGetValue(labId, out string previousGroup))
                {
                    result.Add(Fail(
                        "LAB_GROUP_DUPLICATE",
                        PublishCheckSeverity.Error,
                        $"Multiple groups map to the same lab id '{labId}' ({previousGroup}, {group.Name}).",
                        "groups",
                        "Keep exactly one remote group per lab."));
                }
                else
                {
                    labToGroup[labId] = group.Name;
                }
            }

            if (labToGroup.Count == 0)
            {
                result.Add(Fail(
                    "LAB_GROUP_MISSING",
                    PublishCheckSeverity.Warning,
                    "No group names matched the one-group-per-lab convention.",
                    "groups",
                    "Use the adapter naming convention for lab groups."));
            }
            else
            {
                result.Add(Pass(
                    "LAB_GROUP_MAPPING_OK",
                    $"Detected {labToGroup.Count} convention-compliant lab group mapping(s).",
                    "groups"));
            }
        }

        private static void ValidateBuildTarget(AddressablesValidationResult result)
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (target == BuildTarget.NoTarget)
            {
                result.Add(Fail(
                    "BUILD_TARGET_INVALID",
                    PublishCheckSeverity.Warning,
                    "Active build target is not configured.",
                    "build",
                    "Switch to a valid build target before building content."));
                return;
            }

            result.Add(Pass("BUILD_TARGET_OK", $"Active build target is {target}.", "build"));
        }
#endif

        private static PublishCheckEntry Pass(string code, string message, string scope)
        {
            return new PublishCheckEntry
            {
                code = code,
                severity = PublishCheckSeverity.Info,
                message = message,
                scope = scope,
                passed = true,
            };
        }

        private static PublishCheckEntry Fail(
            string code,
            string severity,
            string message,
            string scope,
            string fixHint = "")
        {
            return new PublishCheckEntry
            {
                code = code,
                severity = severity,
                message = message,
                scope = scope,
                fixHint = fixHint ?? string.Empty,
                passed = false,
            };
        }
    }
}
#endif
