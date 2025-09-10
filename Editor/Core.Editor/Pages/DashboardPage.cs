#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ScenarioSceneManager = Pitech.XR.Scenario.SceneManager;

namespace Pitech.XR.Core.Editor
{
    public sealed class DashboardPage : IDevkitPage
    {
        public string Title => "Dashboard";

        public void BuildUI(VisualElement root)
        {
            root.Add(Section("Project Setup", el =>
            {
                el.Add(Badge(DevkitContext.HasTimeline, "Unity Timeline"));
                el.Add(Badge(DevkitContext.HasTextMeshPro, "TextMeshPro"));
                el.Add(Badge(false, "Scenario module"));
                el.Add(Badge(false, "Stats module"));

                var hint = new HelpBox(
                    "Enable modules you need, then use the actions below to create assets & scene objects.",
                    HelpBoxMessageType.None);
                hint.style.marginTop = 6; el.Add(hint);
            }));

            root.Add(Section("Quick Actions", el =>
            {
                el.Add(WideButton("Create StatsConfig asset", CreateStatsConfigAsset));
                el.Add(WideButton("Create Scene Manager", CreateSceneManager));
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(Button("Create Scenario GameObject", CreateScenarioGO));
                row.Add(new Label("  Scenario Graph available") { style = { color = new Color(0.4f, 1f, 0.5f) } });
                row.Add(new VisualElement { style = { flexGrow = 1 } });
                row.Add(Button("Open Scenario Graph", OpenScenarioGraph));
                el.Add(row);
            }));

            root.Add(Section("Utilities", el =>
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(Button("Open Package Manager", () => EditorApplication.ExecuteMenuItem("Window/Package Manager")));
                row.Add(Button("Reimport All", () => AssetDatabase.ImportAsset("Assets", ImportAssetOptions.ForceUpdate)));
                el.Add(row);
            }));
        }

        // ---------- helpers ----------
        static VisualElement Section(string title, System.Action<VisualElement> fill)
        {
            var box = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.13f, 0.15f, 0.18f, 1f),
                    paddingTop = 10, paddingBottom = 10, paddingLeft = 10, paddingRight = 10,
                    marginBottom = 10, borderTopLeftRadius = 6, borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6, borderBottomRightRadius = 6
                }
            };
            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 6;
            box.Add(label);

            var content = new VisualElement();
            content.style.flexDirection = FlexDirection.Column;
            box.Add(content);
            fill?.Invoke(content);
            return box;
        }

        static Button Button(string text, System.Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.marginRight = 8;
            return b;
        }

        static VisualElement WideButton(string text, System.Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.width = Length.Percent(100);
            b.style.marginBottom = 8;
            return b;
        }

        static VisualElement Badge(bool ok, string label)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var dot = new VisualElement
            {
                style =
                {
                    width = 10, height = 10, borderTopLeftRadius = 5, borderBottomLeftRadius = 5,
                    borderTopRightRadius = 5, borderBottomRightRadius = 5,
                    backgroundColor = ok ? new Color(0.3f, 0.9f, 0.5f) : new Color(0.95f, 0.35f, 0.35f),
                    marginRight = 6
                }
            };
            row.Add(dot);
            row.Add(new Label(label));
            return row;
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
        static void CreateSceneManager()
        {
            var go = new GameObject("Scene Manager");
            go.AddComponent<ScenarioSceneManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create Scene Manager");
            Selection.activeGameObject = go;
        }
        static void OpenScenarioGraph() { EditorApplication.ExecuteMenuItem("Window/General/Inspector"); }
    }
}
#endif
