#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

#if PITECH_ADDR
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build;
#endif

namespace Pitech.XR.ContentDelivery.Editor
{
    [Serializable]
    public sealed class AddressablesBuildResult
    {
        public bool success;
        public bool dryRun;
        public string summary = string.Empty;
        public string outputPath = string.Empty;
        public string uploadPath = string.Empty;
        public string internalBuildPath = string.Empty;
        public string catalogHash = string.Empty;
        public string contentHash = string.Empty;
        public long bundleSizeBytes;
        public List<string> notes = new List<string>();
    }

    public sealed class AddressablesBuildService
    {
        public AddressablesBuildResult Build(AddressablesModuleConfig config, bool dryRun)
        {
            AddressablesBuildResult result = new AddressablesBuildResult
            {
                dryRun = dryRun,
            };

            if (!ContentDeliveryCapability.HasAddressablesPackage)
            {
                result.success = false;
                result.summary = "Addressables package is not installed.";
                return result;
            }

#if PITECH_ADDR
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                result.success = false;
                result.summary = "Addressables settings not found. Run setup first.";
                return result;
            }

            string profileName = string.IsNullOrWhiteSpace(config.profileName) ? "Default" : config.profileName.Trim();
            string profileId = settings.profileSettings.GetProfileId(profileName);
            if (string.IsNullOrEmpty(profileId))
            {
                result.success = false;
                result.summary = $"Profile '{profileName}' does not exist.";
                return result;
            }

            string resolvedUploadPath = ResolveRemoteBuildOutputPath(settings, profileId);
            result.uploadPath = resolvedUploadPath;
            result.outputPath = resolvedUploadPath;

            settings.activeProfileId = profileId;
            if (dryRun)
            {
                result.success = true;
                result.summary = $"Dry-run build ready for profile '{profileName}'.";
                result.notes.Add("No Addressables build executed.");
                if (!string.IsNullOrWhiteSpace(result.uploadPath))
                {
                    result.notes.Add($"Upload folder: {result.uploadPath}");
                }
                return result;
            }

            AddressablesPlayerBuildResult buildResult;
            AddressableAssetSettings.BuildPlayerContent(out buildResult);
            if (!string.IsNullOrWhiteSpace(buildResult.Error))
            {
                result.success = false;
                result.summary = $"Build failed: {buildResult.Error}";
                return result;
            }

            result.internalBuildPath = NormalizeAbsolute(buildResult.OutputPath ?? string.Empty);
            if (string.IsNullOrWhiteSpace(result.outputPath))
            {
                result.outputPath = result.internalBuildPath;
            }
            PopulateHashesAndSizes(result);
            result.success = true;
            result.summary = "Addressables build completed.";
            if (!string.IsNullOrWhiteSpace(result.uploadPath))
            {
                result.notes.Add($"Upload folder: {result.uploadPath}");
            }
            if (!string.IsNullOrWhiteSpace(result.internalBuildPath))
            {
                result.notes.Add($"Internal build path: {result.internalBuildPath}");
            }
#else
            result.success = false;
            result.summary = "PITECH_ADDR define is unavailable. Reimport scripts.";
#endif

            return result;
        }

        private static void PopulateHashesAndSizes(AddressablesBuildResult result)
        {
            if (result == null)
            {
                return;
            }

            string scanPath = ResolveScanPath(result);
            if (string.IsNullOrWhiteSpace(scanPath) || !Directory.Exists(scanPath))
            {
                return;
            }

            string[] files = Directory.GetFiles(scanPath, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (files.Length == 0)
            {
                return;
            }

            long totalSize = 0;
            StringBuilder fingerprint = new StringBuilder();
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                FileInfo info = new FileInfo(file);
                totalSize += info.Length;
                fingerprint.Append(file.Replace("\\", "/"));
                fingerprint.Append('|');
                fingerprint.Append(info.Length);
                fingerprint.Append('|');
                fingerprint.Append(info.LastWriteTimeUtc.Ticks);
                fingerprint.Append(';');
            }

            string contentHash = HashUtf8(fingerprint.ToString());
            result.contentHash = contentHash;
            result.bundleSizeBytes = totalSize;

            string catalogFile = files.FirstOrDefault(
                file => file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                        file.IndexOf("catalog", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(catalogFile))
            {
                result.catalogHash = HashUtf8(File.ReadAllText(catalogFile));
            }
        }

        private static string ResolveScanPath(AddressablesBuildResult result)
        {
            string upload = NormalizeAbsolute(result.uploadPath);
            if (!string.IsNullOrWhiteSpace(upload) && Directory.Exists(upload))
            {
                return upload;
            }

            string output = NormalizeAbsolute(result.outputPath);
            if (!string.IsNullOrWhiteSpace(output) && Directory.Exists(output))
            {
                return output;
            }

            string internalPath = NormalizeAbsolute(result.internalBuildPath);
            if (!string.IsNullOrWhiteSpace(internalPath) && Directory.Exists(internalPath))
            {
                return internalPath;
            }

            return string.Empty;
        }

#if PITECH_ADDR
        private static string ResolveRemoteBuildOutputPath(AddressableAssetSettings settings, string profileId)
        {
            if (settings == null || string.IsNullOrWhiteSpace(profileId))
            {
                return string.Empty;
            }

            string remoteBuildRaw = settings.profileSettings.GetValueByName(profileId, AddressableAssetSettings.kRemoteBuildPath);
            if (string.IsNullOrWhiteSpace(remoteBuildRaw))
            {
                return string.Empty;
            }

            string evaluated = settings.profileSettings.EvaluateString(profileId, remoteBuildRaw);
            return NormalizeAbsolute(evaluated);
        }
#endif

        private static string NormalizeAbsolute(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string candidate = path.Replace("\\", "/").Trim();
            if (Path.IsPathRooted(candidate))
            {
                return Path.GetFullPath(candidate).Replace("\\", "/");
            }

            string projectRoot = Path.GetFullPath(".").Replace("\\", "/");
            return Path.GetFullPath(Path.Combine(projectRoot, candidate)).Replace("\\", "/");
        }

        private static string HashUtf8(string value)
        {
            string safe = value ?? string.Empty;
            using SHA256 sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(safe);
            byte[] hash = sha.ComputeHash(bytes);
            return ToLowerHex(hash);
        }

        private static string ToLowerHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            char[] chars = new char[bytes.Length * 2];
            const string map = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++)
            {
                int b = bytes[i];
                chars[i * 2] = map[b >> 4];
                chars[i * 2 + 1] = map[b & 0x0F];
            }

            return new string(chars);
        }
    }
}
#endif
