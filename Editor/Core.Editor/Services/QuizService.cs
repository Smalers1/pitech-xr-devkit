#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pitech.XR.Core.Editor
{
    public sealed class QuizService
    {
        public UnityEngine.Object CreateAsset()
        {
            var t = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
                .FirstOrDefault(x => x.FullName == "Pitech.XR.Quiz.QuizAsset");
            if (t == null) { EditorUtility.DisplayDialog("Quiz", "QuizAsset type not found.", "OK"); return null; }

            string folder = "Assets";
            var sel = Selection.activeObject ? AssetDatabase.GetAssetPath(Selection.activeObject) : null;
            if (!string.IsNullOrEmpty(sel)) folder = Directory.Exists(sel) ? sel : Path.GetDirectoryName(sel);

            var asset = ScriptableObject.CreateInstance(t);
            var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, "QuizAsset.asset"));
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            return Selection.activeObject;
        }

        public void AddQuizToScene()
        {
            var setup = new GuidedSetupService();
            var scene = setup.ActiveScene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog("Quiz", "Open a scene first.", "OK");
                return;
            }

            setup.EnsureManagersRoot();
            var uiRoot = EnsureRoot(scene, "--- UI ---");

            var sm = setup.FindFirstInScene("Pitech.XR.Scenario.SceneManager") as Component;
            if (!sm)
                sm = setup.CreateUnderManagersRoot("Pitech.XR.Scenario.SceneManager", "Scene Manager", "Create Scene Manager");

            var quizUI = setup.FindFirstInScene("Pitech.XR.Quiz.QuizUIController") as Component;
            if (!quizUI)
            {
                var t = GuidedSetupService.FindType("Pitech.XR.Quiz.QuizUIController");
                if (t == null) { EditorUtility.DisplayDialog("Quiz", "QuizUIController type not found.", "OK"); return; }

                var go = new GameObject("Quiz UI");
                Undo.RegisterCreatedObjectUndo(go, "Create Quiz UI");
                go.transform.SetParent(uiRoot, false);
                quizUI = go.AddComponent(t) as Component;
                EditorSceneManager.MarkSceneDirty(go.scene);
            }

            var asset = CreateAsset();

            if (sm)
            {
                if (asset) setup.AssignObjectProperty(sm, "quiz", asset, "Assign Quiz Asset");
                if (quizUI) setup.AssignObjectProperty(sm, "quizUI", quizUI, "Assign Quiz UI");
                EditorGUIUtility.PingObject(sm);
            }
        }

        static Transform EnsureRoot(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go.transform;

            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create Scene Category");
            EditorSceneManager.MarkSceneDirty(scene);
            return root.transform;
        }
    }
}
#endif
