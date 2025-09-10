#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pitech.XR.Core.Editor
{
    internal sealed class SceneCategoriesService
    {
        public static readonly string[] DefaultCategories = new[]
        {
            "--- LIGHTING ---",
            "--- SCENE MANAGERS ---",
            "--- ENVIRONMENT ---",
            "--- INTERACTABLES ---",
            "--- TIMELINES ---",
            "--- UI ---",
            "--- AUDIO ---",
            "--- VFX ---",
            "--- CAMERAS ---",
            "--- DEBUG ---"
        };

        public void CreateSelected(IEnumerable<string> names)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog("Scene Categories", "No open scene is active.", "OK");
                return;
            }

            int created = 0, skipped = 0;
            foreach (var raw in names.Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                var name = raw.Trim();
                if (RootContains(scene, name)) { skipped++; continue; }

                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create Scene Category");
                var t = go.transform;
                t.position = Vector3.zero; t.rotation = Quaternion.identity; t.localScale = Vector3.one;
                created++;
            }

            if (created > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            EditorUtility.DisplayDialog("Scene Categories",
                $"Created {created} category object(s){(skipped > 0 ? $" and skipped {skipped} existing" : "")}.",
                "OK");
        }

        static bool RootContains(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].name == name) return true;
            return false;
        }
    }
}
#endif
