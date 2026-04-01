#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Core.Editor
{
    /// <summary>Shared editor context/branding helpers (static only).</summary>
    internal static class DevkitContext
    {
        private static string _versionLabel;

        /// <summary>Display label for the DevKit package, e.g. v0.8.1 (from package manifest).</summary>
        public static string Version
        {
            get
            {
                if (_versionLabel == null)
                {
                    _versionLabel = ResolveVersionLabel();
                }

                return _versionLabel;
            }
        }

        static string ResolveVersionLabel()
        {
            string semver = TryReadPackageSemver();
            return string.IsNullOrWhiteSpace(semver) ? "vunknown" : $"v{semver.Trim()}";
        }

        /// <summary>
        /// Semver for this DevKit install. Prefers <c>package.json</c> on disk (under
        /// <see cref="UnityEditor.PackageManager.PackageInfo.resolvedPath"/>) because
        /// <see cref="UnityEditor.PackageManager.PackageInfo.version"/> can lag behind the file after local edits until UPM refreshes.
        /// </summary>
        internal static string TryReadPackageSemver()
        {
            try
            {
                UnityEditor.PackageManager.PackageInfo info =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DevkitContext).Assembly);
                if (info != null)
                {
                    if (!string.IsNullOrWhiteSpace(info.resolvedPath))
                    {
                        string onDisk = TryParseVersionFromPackageJsonPath(
                            Path.Combine(info.resolvedPath, "package.json"));
                        if (!string.IsNullOrWhiteSpace(onDisk))
                        {
                            return onDisk.Trim();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(info.version))
                    {
                        return info.version.Trim();
                    }
                }
            }
            catch
            {
                // Editor without UPM or embedded layout — fall through.
            }

            return TryReadSemverFromKnownPackageJsonPaths();
        }

        static string TryParseVersionFromPackageJsonPath(string packageJsonPath)
        {
            if (string.IsNullOrWhiteSpace(packageJsonPath) || !File.Exists(packageJsonPath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(packageJsonPath);
                Match match = Regex.Match(json, "\"version\"\\s*:\\s*\"(?<value>[^\"]+)\"");
                return match.Success ? match.Groups["value"].Value.Trim() : null;
            }
            catch
            {
                return null;
            }
        }

        static string TryReadSemverFromKnownPackageJsonPaths()
        {
            string[] candidates =
            {
                Path.GetFullPath("Packages/com.pitech.xr.devkit/package.json"),
                Path.GetFullPath("Packages/pitech-xr-devkit/package.json"),
                Path.GetFullPath("Packages/xr.devkit/package.json"),
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string semver = TryParseVersionFromPackageJsonPath(candidates[i]);
                if (!string.IsNullOrWhiteSpace(semver))
                {
                    return semver;
                }
            }

            return null;
        }

        // Title icon (tab) and sidebar logo, resolved by name (you placed them under Editor/Core/Icons)
        public static Texture2D TitleIcon => FindTextureByName("Pi tech Icon");
        public static Texture2D SidebarLogo => FindTextureByName("Pi tech Logo");

        // Capability checks (optional)
        public static bool HasTimeline => Type.GetType("UnityEngine.Timeline.TimelineAsset, UnityEngine.Timeline") != null;
        public static bool HasTextMeshPro => Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro") != null;
        public static bool HasAddressables => Type.GetType(
            "UnityEditor.AddressableAssets.Settings.AddressableAssetSettings, Unity.Addressables.Editor") != null;
        public static bool HasCcdManagement => HasPackage("com.unity.services.ccd.management");

        // Finder
        static Texture2D FindTextureByName(string assetName)
        {
            var guids = AssetDatabase.FindAssets(assetName);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) return tex;
            }
            return null;
        }

        static bool HasPackage(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return false;
            }

            var manifest = System.IO.Path.GetFullPath("Packages/manifest.json");
            if (!System.IO.File.Exists(manifest))
            {
                return false;
            }

            var text = System.IO.File.ReadAllText(manifest);
            return text.IndexOf($"\"{packageName}\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
