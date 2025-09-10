#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Core.Editor
{
    internal sealed class ScenarioService
    {
        public void CreateScenarioGameObject()
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                     .SelectMany(a => a.GetTypes())
                     .FirstOrDefault(x => x.FullName == "Pitech.XR.Scenario.Scenario" ||
                                          x.FullName == "Pitech.XR.ScenarioKit.Scenario" ||
                                          x.Name == "Scenario");
            if (t == null) { EditorUtility.DisplayDialog("Scenario", "Scenario component not found.", "OK"); return; }
            var go = new GameObject("Scenario"); Undo.RegisterCreatedObjectUndo(go, "Create Scenario"); go.AddComponent(t);
            Selection.activeGameObject = go;
        }

        public void OpenGraph()
        {
            var winType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == "ScenarioGraphWindow" && typeof(EditorWindow).IsAssignableFrom(t));
            if (winType == null) { EditorUtility.DisplayDialog("Scenario Graph", "Window not found.", "OK"); return; }
            var w = EditorWindow.GetWindow(winType); w.titleContent = new GUIContent("Scenario Graph"); (w as EditorWindow).Show();
        }
    }
}
#endif
