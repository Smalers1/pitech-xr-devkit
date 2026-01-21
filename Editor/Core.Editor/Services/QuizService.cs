#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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
            var quizCanvas = EnsureQuizCanvas(uiRoot, scene);

            var sm = setup.FindFirstInScene("Pitech.XR.Scenario.SceneManager") as Component;
            if (!sm)
                sm = setup.CreateUnderManagersRoot("Pitech.XR.Scenario.SceneManager", "Scene Manager", "Create Scene Manager");

            // Load default UI prefabs shipped *inside the package* and instantiate them into the scene.
            var quizPanelPrefab = LoadDevkitPrefab("Editor/Quiz.Editor/DefaultUIPrefabs/QuizPanel.prefab");
            var quizResultsPrefab = LoadDevkitPrefab("Editor/Quiz.Editor/DefaultUIPrefabs/QuizResultsPanel.prefab");
            var answerButtonPrefabGo = LoadDevkitPrefab("Editor/Quiz.Editor/DefaultUIPrefabs/QuizAnswerButton.prefab");
            var answerButtonPrefab = answerButtonPrefabGo != null ? answerButtonPrefabGo.GetComponent<UnityEngine.UI.Button>() : null;

            Component quizPanel = setup.FindFirstInScene("Pitech.XR.Quiz.QuizUIController") as Component;
            if (!quizPanel && quizPanelPrefab)
            {
                var inst = PrefabUtility.InstantiatePrefab(quizPanelPrefab) as GameObject;
                if (inst)
                {
                    Undo.RegisterCreatedObjectUndo(inst, "Create Quiz Panel");
                    inst.transform.SetParent(quizCanvas.transform, false);
                    quizPanel = inst.GetComponent("QuizUIController") as Component;

                    // Wire the shared CanvasGroup (on the quiz canvas) into the panel controller.
                    if (quizPanel != null)
                        AssignCanvasGroupIfPresent(quizPanel, quizCanvas);

                    // Ensure the answer button prefab reference is set (prefab may keep it null to avoid cross-asset references).
                    if (quizPanel != null && answerButtonPrefab != null)
                    {
                        var so = new SerializedObject(quizPanel);
                        var p = so.FindProperty("answerButtonPrefab");
                        if (p != null)
                        {
                            so.Update();
                            p.objectReferenceValue = answerButtonPrefab;
                            so.ApplyModifiedProperties();
                            EditorUtility.SetDirty(quizPanel);
                        }
                    }
                    EditorSceneManager.MarkSceneDirty(inst.scene);
                }
            }

            Component quizResultsPanel = setup.FindFirstInScene("Pitech.XR.Quiz.QuizResultsUIController") as Component;
            if (!quizResultsPanel && quizResultsPrefab)
            {
                var inst = PrefabUtility.InstantiatePrefab(quizResultsPrefab) as GameObject;
                if (inst)
                {
                    Undo.RegisterCreatedObjectUndo(inst, "Create Quiz Results Panel");
                    inst.transform.SetParent(quizCanvas.transform, false);
                    quizResultsPanel = inst.GetComponent("QuizResultsUIController") as Component;

                    // Wire the shared CanvasGroup (on the quiz canvas) into the results controller.
                    if (quizResultsPanel != null)
                        AssignCanvasGroupIfPresent(quizResultsPanel, quizCanvas);

                    EditorSceneManager.MarkSceneDirty(inst.scene);
                }
            }

            if (!quizPanelPrefab || !quizResultsPrefab)
            {
                EditorUtility.DisplayDialog(
                    "Quiz",
                    "Default quiz UI prefabs were not found.\n\n" +
                    "Expected (inside the DevKit package):\n" +
                    "- Editor/Quiz.Editor/DefaultUIPrefabs/QuizPanel.prefab\n" +
                    "- Editor/Quiz.Editor/DefaultUIPrefabs/QuizResultsPanel.prefab\n\n" +
                    "Tip: If your package root folder name differs, this installer will still work once the prefabs exist in the package.",
                    "OK");
            }

            var asset = CreateAsset();

            if (sm)
            {
                if (asset) setup.AssignObjectProperty(sm, "defaultQuiz", asset, "Assign Default Quiz");
                if (quizPanel) setup.AssignObjectProperty(sm, "quizPanel", quizPanel, "Assign Quiz Panel");
                if (quizResultsPanel) setup.AssignObjectProperty(sm, "quizResultsPanel", quizResultsPanel, "Assign Quiz Results Panel");
                EditorGUIUtility.PingObject(sm);
            }
        }

        static GameObject EnsureQuizCanvas(Transform uiRoot, UnityEngine.SceneManagement.Scene scene)
        {
            // Look for an existing quiz canvas under UI root.
            for (int i = 0; i < uiRoot.childCount; i++)
            {
                var t = uiRoot.GetChild(i);
                if (t && t.name == "Quiz Canvas")
                {
                    var existing = t.GetComponent<Canvas>();
                    if (existing) return t.gameObject;
                }
            }

            var go = new GameObject("Quiz Canvas");
            Undo.RegisterCreatedObjectUndo(go, "Create Quiz Canvas");
            go.layer = 5; // UI
            go.transform.SetParent(uiRoot, false);

            // Ensure components needed for a functional UI canvas.
            var canvas = Undo.AddComponent<Canvas>(go);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.AddComponent<CanvasScaler>(go);
            Undo.AddComponent<GraphicRaycaster>(go);

            // Shared CanvasGroup used by both panels (so we never disable the whole canvas GameObject).
            var cg = Undo.AddComponent<CanvasGroup>(go);
            cg.alpha = 1f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            EditorSceneManager.MarkSceneDirty(scene);
            return go;
        }

        static void AssignCanvasGroupIfPresent(Component controller, GameObject quizCanvas)
        {
            if (controller == null || quizCanvas == null) return;
            var cg = quizCanvas.GetComponent<CanvasGroup>();
            if (!cg) return;

            var so = new SerializedObject(controller);
            var p = so.FindProperty("canvasGroup");
            if (p == null) return;
            so.Update();
            p.objectReferenceValue = cg;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }

        static GameObject LoadDevkitPrefab(string relativePathFromPackageRoot)
        {
            // Support both embedded folder name and registry package name.
            string[] roots =
            {
                "Packages/pitech-xr-devkit",
                "Packages/com.pitech.xr.devkit",
                // Some Unity versions show the package by displayName in the Project window.
                // Even if it's just a UI label, in some embedded setups the asset path actually uses it.
                "Packages/Pi tech XR DevKit",
                "Packages/Pi tech XR Devkit"
            };

            for (int i = 0; i < roots.Length; i++)
            {
                var path = $"{roots[i]}/{relativePathFromPackageRoot}";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) return prefab;
            }

            // Fallback: search by name, then validate the suffix.
            // This works even when Unity's "Packages" folder isn't indexed in the way we expect.
            string filename = Path.GetFileNameWithoutExtension(relativePathFromPackageRoot);
            var guids = AssetDatabase.FindAssets($"{filename} t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                var p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(p)) continue;
                p = p.Replace('\\', '/');
                if (!p.EndsWith(relativePathFromPackageRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab != null) return prefab;
            }

            // Last resort: scan all asset paths and match by suffix.
            var all = AssetDatabase.GetAllAssetPaths();
            for (int i = 0; i < all.Length; i++)
            {
                var p = all[i];
                if (string.IsNullOrEmpty(p)) continue;
                p = p.Replace('\\', '/');
                if (!p.EndsWith(relativePathFromPackageRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab != null) return prefab;
            }
            return null;
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
