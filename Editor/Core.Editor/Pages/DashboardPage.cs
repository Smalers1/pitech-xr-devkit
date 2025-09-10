#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public sealed class DashboardPage : IDevkitPage
    {
        public string Title => "Dashboard";

        public void BuildUI(VisualElement root)
        {
            root.Add(DevkitTheme.Section("Project Setup", el =>
            {
                el.Add(DevkitTheme.Badge(DevkitContext.HasTimeline, "Unity Timeline"));
                el.Add(DevkitTheme.Badge(DevkitContext.HasTextMeshPro, "TextMeshPro"));
                el.Add(DevkitTheme.Badge(false, "Scenario module"));
                el.Add(DevkitTheme.Badge(false, "Stats module"));

                var hint = new HelpBox(
                    "Enable modules you need, then use the actions below to create assets & scene objects.",
                    HelpBoxMessageType.None);
                hint.style.marginTop = 6; el.Add(hint);
            }));

            root.Add(DevkitTheme.Section("Quick Actions", el =>
            {
                el.Add(DevkitTheme.WideButton("Create StatsConfig asset", CreateStatsConfigAsset));
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(DevkitTheme.Button("Create Scenario GameObject", CreateScenarioGO, b => b.style.marginRight = 8));
                row.Add(new Label("  Scenario Graph available") { style = { color = new Color(0.4f, 1f, 0.5f) } });
                row.Add(new VisualElement { style = { flexGrow = 1 } });
                row.Add(DevkitTheme.Button("Open Scenario Graph", OpenScenarioGraph));
                el.Add(row);
            }));

            root.Add(DevkitTheme.Section("Utilities", el =>
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(DevkitTheme.Button("Open Package Manager", () => EditorApplication.ExecuteMenuItem("Window/Package Manager"), b => b.style.marginRight = 8));
                row.Add(DevkitTheme.Button("Reimport All", () => AssetDatabase.ImportAsset("Assets", ImportAssetOptions.ForceUpdate)));
                el.Add(row);
            }));
        }
        // Actions (replace with your implementations)
        static void CreateStatsConfigAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create StatsConfig", "StatsConfig", "asset", "");
            if (!string.IsNullOrEmpty(path))
            {
                var obj = ScriptableObject.CreateInstance("Pitech.XR.Stats.StatsConfig");
                AssetDatabase.CreateAsset(obj, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = obj;
            }
        }
        static void CreateScenarioGO() { EditorApplication.ExecuteMenuItem("GameObject/Create Empty"); }
        static void OpenScenarioGraph() { EditorApplication.ExecuteMenuItem("Window/General/Inspector"); }
    }
}
#endif
