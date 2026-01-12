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
    /// Scene-agnostic helpers for finding/creating and wiring DevKit scene objects.
    /// Uses reflection to avoid runtime asmdef references.
    /// </summary>
    internal sealed class GuidedSetupService
    {
        const string ManagersRootName = "--- SCENE MANAGERS ---";

        public Scene ActiveScene => SceneManager.GetActiveScene();

        public bool HasActiveSceneLoaded()
        {
            var s = ActiveScene;
            return s.IsValid() && s.isLoaded;
        }

        public Transform EnsureManagersRoot()
        {
            var s = ActiveScene;
            if (!s.IsValid() || !s.isLoaded) return null;

            var root = s.GetRootGameObjects().FirstOrDefault(g => g.name == ManagersRootName);
            if (!root)
            {
                root = new GameObject(ManagersRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Managers Root");
                EditorSceneManager.MarkSceneDirty(s);
            }
            return root.transform;
        }

        public static Type FindType(string fullName)
            => AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.FullName == fullName);

        public Component FindFirstInScene(string fullTypeName)
        {
            var t = FindType(fullTypeName);
            if (t == null) return null;

            // FindObjectsOfTypeAll is editor-safe and can find inactive objects.
            var objs = Resources.FindObjectsOfTypeAll(t);
            for (int i = 0; i < objs.Length; i++)
            {
                if (objs[i] is Component c && c.gameObject.scene == ActiveScene)
                    return c;
            }
            return null;
        }

        public Component CreateUnderManagersRoot(string fullTypeName, string goName, string undoName)
        {
            var t = FindType(fullTypeName);
            if (t == null)
            {
                EditorUtility.DisplayDialog("DevKit", $"Type not found: {fullTypeName}", "OK");
                return null;
            }

            var parent = EnsureManagersRoot();
            if (!parent)
            {
                EditorUtility.DisplayDialog("DevKit", "Open a scene first (e.g. Assets/Scenes/Testing).", "OK");
                return null;
            }

            var go = new GameObject(goName);
            Undo.RegisterCreatedObjectUndo(go, undoName);
            go.transform.SetParent(parent, false);
            var comp = go.AddComponent(t) as Component;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeObject = go;
            return comp;
        }

        public void AssignObjectProperty(Component targetComponent, string propertyName, UnityEngine.Object value, string undoName)
        {
            if (!targetComponent) return;

            var so = new SerializedObject(targetComponent);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                EditorUtility.DisplayDialog("DevKit", $"Property not found: {targetComponent.GetType().Name}.{propertyName}", "OK");
                return;
            }

            Undo.RecordObject(targetComponent, undoName);
            so.Update();
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(targetComponent);
            EditorSceneManager.MarkSceneDirty(targetComponent.gameObject.scene);
        }
    }
}
#endif


