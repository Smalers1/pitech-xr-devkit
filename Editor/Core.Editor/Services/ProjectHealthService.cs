#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pitech.XR.Core.Editor
{
    internal sealed class ProjectHealthService
    {
        static readonly string[] RequiredFolders = new[]
        {
            "Assets/3D Models/Environment","Assets/3D Models/Avatars","Assets/3D Models/Tools",
            "Assets/Animations","Assets/Materials",
            "Assets/Audio/Music","Assets/Audio/SFX",
            "Assets/Textures","Assets/Shaders","Assets/Prefabs","Assets/Resources",
            "Assets/Scenes",
            "Assets/Scripts/Core","Assets/Scripts/Gameplay","Assets/Scripts/UI","Assets/Scripts/Editor",
            "Assets/Settings","Assets/UI/Menu","Assets/UI/Logo","Assets/UI/Images"
        };

        static readonly string[] RecommendedSceneRoots = new[]
        {
            "--- LIGHTING ---","--- SCENE MANAGERS ---","--- ENVIRONMENT ---","--- INTERACTABLES ---",
            "--- TIMELINES ---","--- UI ---","--- AUDIO ---","--- VFX ---","--- CAMERAS ---","--- DEBUG ---"
        };

        public (int ok, int total, string[] missing) CheckFolders()
        {
            var missing = RequiredFolders.Where(p => !AssetDatabase.IsValidFolder(p)).ToArray();
            return (RequiredFolders.Length - missing.Length, RequiredFolders.Length, missing);
        }

        public (int ok, int total, string[] missing) CheckSceneRoots()
        {
            var s = SceneManager.GetActiveScene();
            if (!s.IsValid() || !s.isLoaded) return (0, RecommendedSceneRoots.Length, RecommendedSceneRoots);
            var names = s.GetRootGameObjects().Select(g => g.name).ToHashSet();
            var missing = RecommendedSceneRoots.Where(n => !names.Contains(n)).ToArray();
            return (RecommendedSceneRoots.Length - missing.Length, RecommendedSceneRoots.Length, missing);
        }

        public (int ok, int total, string[] missing) CheckSettings()
        {
            int ok = 0; var missing = new System.Collections.Generic.List<string>();
            if (PlayerSettings.colorSpace == ColorSpace.Linear) ok++; else missing.Add("Color space: Linear");
            if (EditorSettings.serializationMode == SerializationMode.ForceText) ok++; else missing.Add("Asset serialization: Force Text");
            if (EditorSettings.externalVersionControl == "Visible Meta Files") ok++; else missing.Add("Version Control: Visible Meta Files");
            return (ok, 3, missing.ToArray());
        }

        public (bool timeline, bool tmp) CheckModules()
        {
            return (DevkitContext.HasTimeline, DevkitContext.HasTextMeshPro);
        }

        public float OverallProgress01()
        {
            var f = CheckFolders();
            var r = CheckSceneRoots();
            var s = CheckSettings();
            float completed = f.ok + r.ok + s.ok;
            float total = f.total + r.total + s.total;
            if (total <= 0f) return 0f;
            return Mathf.Clamp01(completed / total);
        }

        // One-click helpers
        public void FixRecommended() => new ProjectSetupService().ApplyRecommendedSettings();
        public void CreateMissingFolders() => new ProjectSetupService().SetupFolders();
    }
}
#endif
