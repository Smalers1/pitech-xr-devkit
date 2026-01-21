using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Editor.Quiz
{
    public static class QuizDefaultUIPrefabFactory
    {
        const string DestFolder = "Assets/Pi tech/Default Quiz UI";

        [MenuItem("Pi tech/Quiz/Copy Default Quiz UI Prefabs to Project (Editable)")]
        public static void CopyDefaultPrefabsToProject()
        {
            EnsureFolder(DestFolder);

            string pkgRootA = "Packages/pitech-xr-devkit";
            string pkgRootB = "Packages/com.pitech.xr.devkit";

            string srcDir = $"{pkgRootA}/Editor/Quiz.Editor/DefaultUIPrefabs";
            if (!AssetDatabase.IsValidFolder(srcDir))
                srcDir = $"{pkgRootB}/Editor/Quiz.Editor/DefaultUIPrefabs";

            if (!AssetDatabase.IsValidFolder(srcDir))
            {
                EditorUtility.DisplayDialog(
                    "Quiz UI",
                    "Could not find default prefabs inside the DevKit package.\n\nExpected folder:\nEditor/Quiz.Editor/DefaultUIPrefabs",
                    "OK");
                return;
            }

            CopyPrefab(srcDir, "QuizPanel.prefab");
            CopyPrefab(srcDir, "QuizResultsPanel.prefab");
            CopyPrefab(srcDir, "QuizAnswerButton.prefab");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Quiz UI Prefabs Copied",
                $"Copied to:\n{DestFolder}\n\nYou can now tweak these prefabs freely in your project.",
                "OK");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            string cur = parts[0]; // Assets
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        static void CopyPrefab(string srcDir, string filename)
        {
            string src = $"{srcDir}/{filename}";
            string dst = $"{DestFolder}/{filename}";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dst) != null)
                return; // keep user's edited copy

            AssetDatabase.CopyAsset(src, dst);
        }
    }
}


