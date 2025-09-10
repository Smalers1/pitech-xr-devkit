// Runtime/Core/XRServices.cs
//
// Small runtime helper for feature/probe checks so other modules can
// remain optional. Keep this file free of any top-level statements.

using System;

namespace Pitech.XR.Core
{
    /// <summary>
    /// Centralized “is it available?” probes for optional modules and third-party packages.
    /// Pure runtime (no #if UNITY_EDITOR guards) so it’s safe in builds.
    /// </summary>
    public static class XRServices
    {
        // --- Unity built-ins / common packages ---

        /// <summary>Unity Timeline available (PlayableDirector type exists)</summary>
        public static bool TimelineAvailable =>
            Type.GetType("UnityEngine.Playables.PlayableDirector, UnityEngine.CoreModule") != null;

        /// <summary>TextMeshPro available (TMP_Text type exists)</summary>
        public static bool TextMeshProAvailable =>
            Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro") != null;

        /// <summary>XR Interaction Toolkit present (XRBaseInteractable type exists)</summary>
        public static bool XRITKAvailable =>
            Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable, Unity.XR.Interaction.Toolkit") != null;

        // --- Our modules (keep names aligned with asmdef namespaces) ---

        /// <summary>Scenario module runtime present.</summary>
        public static bool ScenarioAvailable =>
            Type.GetType("Pitech.XR.Scenario.Scenario, Pitech.XR.Scenario") != null;

        /// <summary>Stats module runtime present.</summary>
        public static bool StatsAvailable =>
            Type.GetType("Pitech.XR.Stats.StatsRuntime, Pitech.XR.Stats") != null;

        // --- Editor-only probes (safe to call at runtime: they just return false) ---

        /// <summary>GraphView (old) exists in editor installs.</summary>
        public static bool EditorGraphViewAvailable =>
            Type.GetType("UnityEditor.Experimental.GraphView.GraphView, UnityEditor") != null;

        /// <summary>UI Toolkit editor window types (base availability check).</summary>
        public static bool EditorUIToolkitAvailable =>
            Type.GetType("UnityEditor.UIElements.Toolbar, UnityEditor") != null;
    }
}
