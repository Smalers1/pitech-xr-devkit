#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pitech.XR.Core.Editor
{
    /// <summary>
    /// Creates scene-level managers under the recommended root group.
    /// </summary>
    internal sealed class SceneManagerService
    {
        const string ManagersRootName = "--- SCENE MANAGERS ---";

        static Transform EnsureManagersRoot()
        {
            var s = SceneManager.GetActiveScene();
            if (!s.IsValid() || !s.isLoaded)
            {
                EditorUtility.DisplayDialog("Create Manager", "Open a scene first.", "OK");
                return null;
            }

            var root = s.GetRootGameObjects().FirstOrDefault(g => g.name == ManagersRootName);
            if (!root)
            {
                root = new GameObject(ManagersRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create scene root");
                EditorSceneManager.MarkSceneDirty(s);
            }
            return root.transform;
        }

        public void CreateSceneManager()
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(x => x.FullName == "Pitech.XR.Scenario.SceneManager" ||    // runtime type
                                         (x.Name == "SceneManager" && x.Namespace == "Pitech.XR.Scenario"));
            if (t == null)
            {
                EditorUtility.DisplayDialog("Scene Manager", "Runtime component 'Pitech.XR.Scenario.SceneManager' not found.", "OK");
                return;
            }

            var parent = EnsureManagersRoot();
            if (!parent) return;

            var go = new GameObject("Scene Manager");
            Undo.RegisterCreatedObjectUndo(go, "Create Scene Manager");
            go.AddComponent(t);
            go.transform.SetParent(parent, false);
            Selection.activeGameObject = go;
        }

        public void CreateStatsUIController()
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(x => x.FullName == "Pitech.XR.Stats.StatsUIController");
            if (t == null)
            {
                EditorUtility.DisplayDialog("Stats UI", "Runtime component 'Pitech.XR.Stats.StatsUIController' not found.", "OK");
                return;
            }

            var parent = EnsureManagersRoot();
            if (!parent) return;

            var go = new GameObject("StatsUIController");
            Undo.RegisterCreatedObjectUndo(go, "Create StatsUIController");
            go.AddComponent(t);
            go.transform.SetParent(parent, false);
            Selection.activeGameObject = go;
        }
    }
}
#endif
