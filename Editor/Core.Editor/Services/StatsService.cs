#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Core.Editor
{
    internal sealed class StatsService
    {
        public void CreateConfig()
        {
            var t = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
                .FirstOrDefault(x => x.FullName == "Pitech.XR.Stats.StatsConfig");
            if (t == null) { EditorUtility.DisplayDialog("Stats", "StatsConfig type not found.", "OK"); return; }

            string folder = "Assets";
            var sel = Selection.activeObject ? AssetDatabase.GetAssetPath(Selection.activeObject) : null;
            if (!string.IsNullOrEmpty(sel)) folder = Directory.Exists(sel) ? sel : Path.GetDirectoryName(sel);

            var asset = ScriptableObject.CreateInstance(t);
            var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, "StatsConfig.asset"));
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        }
    }
}
#endif
