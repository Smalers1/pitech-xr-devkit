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
        public static readonly string Version = "v0.3.0";

        // Title icon (tab) and sidebar logo, resolved by name (you placed them under Editor/Core/Icons)
        public static Texture2D TitleIcon => FindTextureByName("Pi tech Icon");
        public static Texture2D SidebarLogo => FindTextureByName("Pi tech Logo");

        // Capability checks (optional)
        public static bool HasTimeline => Type.GetType("UnityEngine.Timeline.TimelineAsset, UnityEngine.Timeline") != null;
        public static bool HasTextMeshPro => Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro") != null;

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
    }
}
#endif
