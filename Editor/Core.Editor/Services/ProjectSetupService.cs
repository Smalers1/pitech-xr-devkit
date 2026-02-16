#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pitech.XR.Core.Editor
{
    internal sealed class ProjectSetupService
    {
        public void SetupFolders()
        {
            int c = 0;
            c += Ensure("Assets/3D Models/Environment");
            c += Ensure("Assets/3D Models/Avatars");
            c += Ensure("Assets/3D Models/Tools");
            c += Ensure("Assets/Animations");
            c += Ensure("Assets/Materials");
            c += Ensure("Assets/Audio/Music");
            c += Ensure("Assets/Audio/SFX");
            c += Ensure("Assets/Textures");
            c += Ensure("Assets/Shaders");
            c += Ensure("Assets/Prefabs");
            c += Ensure("Assets/Resources");
            c += Ensure("Assets/Scenes");
            c += Ensure("Assets/Scripts/Core");
            c += Ensure("Assets/Scripts/Gameplay");
            c += Ensure("Assets/Scripts/UI");
            c += Ensure("Assets/Scripts/Editor");
            c += Ensure("Assets/Settings");
            c += Ensure("Assets/UI/Menu");
            c += Ensure("Assets/UI/Logo");
            c += Ensure("Assets/UI/Images");

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Project Setup", $"Created/ensured {c} folders.", "OK");
        }

        public void ApplyRecommendedSettings()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Try to import TMP essentials if available
            var t = System.Type.GetType("TMPro.TMP_PackageUtilities, Unity.TextMeshPro.Editor");
            t?.GetMethod("ImportEssentialResources", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)
              ?.Invoke(null, null);
        }

        public void CreateMainScene()
        {
            const string path = "Assets/Scenes/Main.unity";
            if (File.Exists(path)) { EditorUtility.DisplayDialog("Main Scene", "Already exists.", "OK"); return; }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            var s = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(s, path);
            EditorSceneManager.CloseScene(s, true);
            EditorSceneManager.RestoreSceneManagerSetup(setup);
            AssetDatabase.Refresh();
        }

        int Ensure(string path)
        {
            var parts = path.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets") return 0;
            string acc = "Assets"; int made = 0;
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{acc}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) { AssetDatabase.CreateFolder(acc, parts[i]); made++; }
                acc = next;
            }
            return made;
        }
    }
}
#endif
