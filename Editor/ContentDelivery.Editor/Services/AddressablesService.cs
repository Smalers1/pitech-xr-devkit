#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

#if PITECH_ADDR
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
#endif

namespace Pitech.XR.ContentDelivery.Editor
{
    [Serializable]
    public sealed class AddressablesSetupResult
    {
        public bool success;
        public bool createdConfigAsset;
        public bool createdSettingsAsset;
        public bool createdProfile;
        public bool createdRemoteGroup;
        public string summary = string.Empty;
        public string configAssetPath = string.Empty;
        public string profileName = string.Empty;
        public string remoteGroupName = string.Empty;
        public string capabilitySummary = string.Empty;
        public List<string> notes = new List<string>();
    }

    [Serializable]
    public sealed class AddressablesMarkFolderResult
    {
        public bool success;
        public bool dryRun;
        public string summary = string.Empty;
        public string folderPath = string.Empty;
        public string groupName = string.Empty;
        public int consideredAssets;
        public int changedEntries;
        public List<string> preview = new List<string>();
    }

    [Serializable]
    public sealed class AddressablesMarkPrefabResult
    {
        public bool success;
        public bool dryRun;
        public bool createdRemoteGroup;
        public string summary = string.Empty;
        public string prefabAssetPath = string.Empty;
        public string prefabGuid = string.Empty;
        public string groupName = string.Empty;
        public string addressKey = string.Empty;
    }

    public sealed class AddressablesService
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string ContentDeliverySettingsFolder = "Assets/Settings/ContentDelivery";
        private const string DefaultRemoteBaseUrl = "https://cdn.example.invalid/content";
        private const string DefaultRemoteLoadPathTemplate = "{baseUrl}/{environment}/[BuildTarget]";

        public AddressablesSetupResult EnsureInitialized(string labIdHint)
        {
            AddressablesSetupResult result = new AddressablesSetupResult
            {
                capabilitySummary = ContentDeliveryCapability.GetCapabilitySummary(),
            };

            AddressablesModuleConfig config = EnsureConfigAsset(
                out bool createdConfigAsset,
                out string configPath);
            result.createdConfigAsset = createdConfigAsset;
            result.configAssetPath = configPath;

            if (!ContentDeliveryCapability.HasAddressablesPackage)
            {
                result.success = false;
                result.summary = "Addressables package is not installed. Install com.unity.addressables first.";
                result.notes.Add("Setup skipped because Addressables capability is unavailable.");
                return result;
            }

#if PITECH_ADDR
            AddressableAssetSettings settings = EnsureAddressablesSettings(out bool createdSettings);
            result.createdSettingsAsset = createdSettings;
            if (settings == null)
            {
                result.success = false;
                result.summary = "Addressables settings could not be initialized.";
                return result;
            }

            EnsureDefineSymbol("PITECH_ADDR");
            string selectedProfile = EnsureProfile(settings, config, out bool createdProfile);
            result.profileName = selectedProfile;
            result.createdProfile = createdProfile;

            IAddressablesConventionAdapter adapter = AddressablesAdapterResolver.Resolve(config);
            string resolvedLabId = string.IsNullOrWhiteSpace(labIdHint) ? "default" : labIdHint.Trim();
            string groupName = adapter.BuildGroupName(config, resolvedLabId);
            result.remoteGroupName = groupName;
            result.createdRemoteGroup = EnsureRemoteGroup(settings, groupName);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            result.success = true;
            result.summary = "Addressables setup completed.";
            if (result.createdConfigAsset)
            {
                result.notes.Add("Created AddressablesModuleConfig asset.");
            }
            if (result.createdSettingsAsset)
            {
                result.notes.Add("Created Addressables settings asset.");
            }
            if (result.createdProfile)
            {
                result.notes.Add($"Created profile '{selectedProfile}'.");
            }
            if (result.createdRemoteGroup)
            {
                result.notes.Add($"Created remote group '{groupName}'.");
            }
#else
            result.success = false;
            result.summary = "Addressables package is installed but compile define PITECH_ADDR is unavailable.";
            result.notes.Add("Reimport scripts so versionDefines can resolve PITECH_ADDR.");
#endif

            return result;
        }

        public AddressablesModuleConfig EnsureConfigAsset(out bool created, out string assetPath)
        {
            created = false;
            assetPath = string.Empty;
            string[] guids = AssetDatabase.FindAssets("t:AddressablesModuleConfig");
            if (guids.Length > 0)
            {
                assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                AddressablesModuleConfig existing =
                    AssetDatabase.LoadAssetAtPath<AddressablesModuleConfig>(assetPath);
                if (existing != null)
                {
                    return existing;
                }
            }

            EnsureFolder(SettingsFolder);
            EnsureFolder(ContentDeliverySettingsFolder);

            AddressablesModuleConfig createdAsset = ScriptableObject.CreateInstance<AddressablesModuleConfig>();
            assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{ContentDeliverySettingsFolder}/AddressablesModuleConfig.asset");
            AssetDatabase.CreateAsset(createdAsset, assetPath);
            AssetDatabase.SaveAssets();
            created = true;
            return createdAsset;
        }

        public bool EnsureDefineSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return false;
            }

            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            string[] chunks = current.Split(';', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < chunks.Length; i++)
            {
                if (string.Equals(chunks[i].Trim(), symbol.Trim(), StringComparison.Ordinal))
                {
                    return false;
                }
            }

            string updated = string.IsNullOrWhiteSpace(current)
                ? symbol.Trim()
                : $"{current};{symbol.Trim()}";
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, updated);
            return true;
        }

        public bool HasInitializedAddressablesSettings()
        {
            if (!ContentDeliveryCapability.HasAddressablesPackage)
            {
                return false;
            }

#if PITECH_ADDR
            return AddressableAssetSettingsDefaultObject.Settings != null;
#else
            return false;
#endif
        }

        public AddressablesMarkFolderResult MarkFolderAddressable(
            string folderPath,
            string groupName,
            bool dryRun)
        {
            AddressablesMarkFolderResult result = new AddressablesMarkFolderResult
            {
                dryRun = dryRun,
                folderPath = folderPath ?? string.Empty,
                groupName = groupName ?? string.Empty,
            };

            if (!ContentDeliveryCapability.HasAddressablesPackage)
            {
                result.success = false;
                result.summary = "Addressables package not installed.";
                return result;
            }

#if PITECH_ADDR
            if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                result.success = false;
                result.summary = "Folder does not exist.";
                return result;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                result.success = false;
                result.summary = "Addressables settings unavailable.";
                return result;
            }

            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                result.success = false;
                result.summary = $"Group '{groupName}' not found.";
                return result;
            }

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folderPath });
            result.consideredAssets = guids.Length;
            for (int i = 0; i < guids.Length; i++)
            {
                string guid = guids[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                result.preview.Add(path);
                if (dryRun)
                {
                    continue;
                }

                settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                result.changedEntries++;
            }

            if (!dryRun)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            result.success = true;
            result.summary = dryRun
                ? $"Previewed {result.preview.Count} assets."
                : $"Marked {result.changedEntries} assets as addressable.";
#else
            result.success = false;
            result.summary = "PITECH_ADDR define is unavailable. Reimport scripts.";
#endif

            return result;
        }

        public AddressablesMarkPrefabResult MarkPrefabAddressable(
            AddressablesModuleConfig config,
            string labIdHint,
            GameObject prefabAsset,
            bool dryRun)
        {
            AddressablesMarkPrefabResult result = new AddressablesMarkPrefabResult
            {
                dryRun = dryRun
            };

            if (!ContentDeliveryCapability.HasAddressablesPackage)
            {
                result.success = false;
                result.summary = "Addressables package not installed.";
                return result;
            }

#if PITECH_ADDR
            if (prefabAsset == null)
            {
                result.success = false;
                result.summary = "Prefab asset is missing.";
                return result;
            }

            string assetPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                result.success = false;
                result.summary = "Prefab asset path could not be resolved.";
                return result;
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                result.success = false;
                result.summary = "Prefab GUID could not be resolved.";
                return result;
            }

            string resolvedLabId = string.IsNullOrWhiteSpace(labIdHint) ? "default" : labIdHint.Trim();
            IAddressablesConventionAdapter adapter = AddressablesAdapterResolver.Resolve(config);
            string groupName = adapter.BuildGroupName(config, resolvedLabId);
            string addressKey = BuildDefaultPrefabAddressKey(resolvedLabId, prefabAsset.name);
            result.prefabAssetPath = assetPath;
            result.prefabGuid = guid;
            result.groupName = groupName;
            result.addressKey = addressKey;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(!dryRun);
            if (settings == null)
            {
                if (dryRun)
                {
                    result.success = true;
                    result.summary = "Preview key is ready. Run Setup to initialize Addressables settings.";
                    return result;
                }

                result.success = false;
                result.summary = "Addressables settings are not available. Run Setup first.";
                return result;
            }

            AddressableAssetGroup group = settings.FindGroup(groupName);
            bool createdGroup = false;
            if (group == null)
            {
                if (dryRun)
                {
                    createdGroup = true;
                }
                else
                {
                    createdGroup = EnsureRemoteGroup(settings, groupName);
                    group = settings.FindGroup(groupName);
                    if (group == null)
                    {
                        result.success = false;
                        result.summary = $"Could not create/find group '{groupName}'.";
                        return result;
                    }
                }
            }

            if (!dryRun)
            {
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                if (entry == null)
                {
                    result.success = false;
                    result.summary = "Could not create or move Addressables entry for prefab.";
                    return result;
                }

                entry.address = addressKey;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            result.success = true;
            result.createdRemoteGroup = createdGroup;
            result.summary = dryRun
                ? "Prefab mapping preview is ready."
                : "Prefab was mapped as lab addressable.";
#else
            result.success = false;
            result.summary = "PITECH_ADDR define is unavailable. Reimport scripts.";
#endif

            return result;
        }

        public bool IsPrefabMapped(
            AddressablesModuleConfig config,
            string labIdHint,
            GameObject prefabAsset,
            out string groupName,
            out string addressKey)
        {
            groupName = string.Empty;
            addressKey = string.Empty;

            if (!ContentDeliveryCapability.HasAddressablesPackage || prefabAsset == null)
            {
                return false;
            }

#if PITECH_ADDR
            string assetPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                return false;
            }

            string resolvedLabId = string.IsNullOrWhiteSpace(labIdHint) ? "default" : labIdHint.Trim();
            IAddressablesConventionAdapter adapter = AddressablesAdapterResolver.Resolve(config);
            groupName = adapter.BuildGroupName(config, resolvedLabId);
            addressKey = BuildDefaultPrefabAddressKey(resolvedLabId, prefabAsset.name);

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return false;
            }

            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            if (entry == null || entry.parentGroup == null)
            {
                return false;
            }

            bool groupMatches = string.Equals(entry.parentGroup.Name, groupName, StringComparison.Ordinal);
            bool keyMatches = string.Equals(entry.address, addressKey, StringComparison.Ordinal);
            return groupMatches && keyMatches;
#else
            return false;
#endif
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

        /// <summary>
        /// Returns the canonical Addressables address key for a lab's main prefab.
        /// Public so that report services can include it in build reports.
        /// </summary>
        public static string ComputeAddressKey(string labId)
        {
            string normalizedLabId = NormalizeKeySegment(labId, "default");
            return $"lab/{normalizedLabId}/prefab/main".ToLowerInvariant();
        }

        private static string BuildDefaultPrefabAddressKey(string labId, string prefabName)
        {
            return ComputeAddressKey(labId);
        }

        private static string NormalizeKeySegment(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string trimmed = value.Trim();
            char[] chars = new char[trimmed.Length];
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                chars[i] = char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-';
            }

            string normalized = new string(chars).Trim('-');
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

#if PITECH_ADDR
        private static AddressableAssetSettings EnsureAddressablesSettings(out bool created)
        {
            created = false;
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                return settings;
            }

            settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            created = settings != null;
            return settings;
        }

        private static string EnsureProfile(
            AddressableAssetSettings settings,
            AddressablesModuleConfig config,
            out bool created)
        {
            created = false;
            string profileName = string.IsNullOrWhiteSpace(config.profileName) ? "Default" : config.profileName.Trim();

            string profileId = settings.profileSettings.GetProfileId(profileName);
            if (string.IsNullOrEmpty(profileId))
            {
                string baseProfileId = settings.profileSettings.GetProfileId("Default");
                profileId = settings.profileSettings.AddProfile(profileName, baseProfileId);
                created = true;
            }

            string remoteBuildPath = BuildRemoteBuildPath(config, profileName);
            string remoteLoadPath = BuildRemoteLoadPath(config);
            settings.profileSettings.SetValue(profileId, AddressableAssetSettings.kRemoteBuildPath, remoteBuildPath);
            settings.profileSettings.SetValue(profileId, AddressableAssetSettings.kRemoteLoadPath, remoteLoadPath);

            return profileName;
        }

        private static string BuildRemoteLoadPath(AddressablesModuleConfig config)
        {
            if (config == null)
            {
                return $"{DefaultRemoteBaseUrl}/development/[BuildTarget]";
            }

            string env = config.environment.ToString().ToLowerInvariant();
            string baseUrl = string.IsNullOrWhiteSpace(config.remoteCatalogBaseUrl)
                ? DefaultRemoteBaseUrl
                : config.remoteCatalogBaseUrl.Trim().TrimEnd('/');

            string template = string.IsNullOrWhiteSpace(config.remoteLoadPathTemplate)
                ? DefaultRemoteLoadPathTemplate
                : config.remoteLoadPathTemplate.Trim();

            return template
                .Replace("{baseUrl}", baseUrl)
                .Replace("{environment}", env);
        }

        private static string BuildRemoteBuildPath(AddressablesModuleConfig config, string profileName)
        {
            string workspaceRoot = "Build/ContentDelivery";
            if (config != null && !string.IsNullOrWhiteSpace(config.localWorkspaceRoot))
            {
                workspaceRoot = NormalizeProjectRelativePath(config.localWorkspaceRoot, "Build/ContentDelivery");
            }

            string safeProfile = string.IsNullOrWhiteSpace(profileName) ? "Default" : profileName.Trim();
            return $"{workspaceRoot}/Addressables/{safeProfile}/[BuildTarget]";
        }

        private static string NormalizeProjectRelativePath(string path, string fallback)
        {
            string raw = string.IsNullOrWhiteSpace(path) ? fallback : path.Trim();
            string normalized = raw.Replace("\\", "/").Trim('/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return fallback;
            }

            return normalized;
        }

        private static bool EnsureRemoteGroup(AddressableAssetSettings settings, string groupName)
        {
            AddressableAssetGroup group = settings.FindGroup(groupName);
            bool created = false;
            if (group == null)
            {
                created = true;
                group = settings.CreateGroup(
                    groupName,
                    false,
                    false,
                    false,
                    null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));
            }

            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled == null)
            {
                bundled = group.AddSchema<BundledAssetGroupSchema>();
            }

            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);

            ContentUpdateGroupSchema contentUpdate = group.GetSchema<ContentUpdateGroupSchema>();
            if (contentUpdate == null)
            {
                group.AddSchema<ContentUpdateGroupSchema>();
            }

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(group);
            return created;
        }
#endif
    }
}
#endif




