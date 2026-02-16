#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;

namespace Pitech.XR.ContentDelivery.Editor
{
    public static class ContentDeliveryCapability
    {
        private const string AddressablesTypeName =
            "UnityEditor.AddressableAssets.Settings.AddressableAssetSettings, Unity.Addressables.Editor";

        private const string CcdPackageName = "com.unity.services.ccd.management";
        private const string AddressablesPackageName = "com.unity.addressables";

        public static bool HasAddressablesPackage =>
            Type.GetType(AddressablesTypeName, throwOnError: false) != null ||
            ManifestContainsPackage(AddressablesPackageName);

        public static bool HasCcdPackage => ManifestContainsPackage(CcdPackageName);

        public static string GetCapabilitySummary()
        {
            return $"Addressables={(HasAddressablesPackage ? "yes" : "no")}, CCD={(HasCcdPackage ? "yes" : "no")}";
        }

        public static bool HasCompilationDefine(string define)
        {
            if (string.IsNullOrWhiteSpace(define))
            {
                return false;
            }

            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            string[] chunks = symbols.Split(';', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < chunks.Length; i++)
            {
                if (string.Equals(chunks[i].Trim(), define.Trim(), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ManifestContainsPackage(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return false;
            }

            string manifestPath = Path.GetFullPath("Packages/manifest.json");
            if (!File.Exists(manifestPath))
            {
                return false;
            }

            string manifest = File.ReadAllText(manifestPath);
            return manifest.IndexOf($"\"{packageName}\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
