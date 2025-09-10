// Packages/com.pitech.xr.devkit/Editor/Core.Editor/Hub/DevkitContext.cs
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Core.Editor
{
    /// <summary>Shared context/state for the DevKit Hub.</summary>
    internal static class DevkitContext
    {
        public const string Version = "v0.1.0 (preview)";

        // Optional: assign your textures in code or load by name if you import them.
        // Keep null-safe usage in UI.
        public static Texture2D TitleIcon;    // small 16-20px icon in title bar
        public static Texture2D SidebarLogo;  // wide logo shown at the top of sidebar
    }
}
