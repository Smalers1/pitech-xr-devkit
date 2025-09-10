#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public sealed class ModulesPage : IDevkitPage
    {
        public string Title => "Modules";

        public void BuildUI(VisualElement root)
        {
            root.Add(Section("Scenario", el =>
            {
                el.Add(new Label("Node-based flow of steps (Timeline → Cue Cards → Questions)."));
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(new Label("Enabled")); row.Add(new VisualElement { style = { width = 8 } });
                row.Add(Button("Open", () => EditorApplication.ExecuteMenuItem("Window/General/Inspector")));
                el.Add(row);
            }));

            root.Add(Section("Stats", el =>
            {
                el.Add(new Label("Configurable KPI system with runtime values and optional UI bindings."));
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(new Label("Enabled")); row.Add(new VisualElement { style = { width = 8 } });
                row.Add(Button("Open", () => EditorApplication.ExecuteMenuItem("Window/General/Project Settings")));
                el.Add(row);
            }));
        }

        // Helpers
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
            var label = new Label(title) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } };
            box.Add(label);
            var content = new VisualElement();
            box.Add(content);
            fill?.Invoke(content);
            return box;
        }

        static Button Button(string text, System.Action onClick) => new Button(onClick) { text = text };
    }
}
#endif
