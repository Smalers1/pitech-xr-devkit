#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Core.Editor
{
    /// <summary>Shared editor context/branding helpers (static only).</summary>
    internal static class DevkitContext
    {
        public static readonly string Version = "v0.6.0";

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
