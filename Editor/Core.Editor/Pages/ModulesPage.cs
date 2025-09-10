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
            root.Add(DevkitTheme.Section("Scenario", el =>
            {
                el.Add(new Label("Node-based flow of steps (Timeline → Cue Cards → Questions)."));
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(new Label("Enabled")); row.Add(new VisualElement { style = { width = 8 } });
                row.Add(DevkitTheme.Button("Open", () => EditorApplication.ExecuteMenuItem("Window/General/Inspector")));
                el.Add(row);
            }));

            root.Add(DevkitTheme.Section("Stats", el =>
            {
                el.Add(new Label("Configurable KPI system with runtime values and optional UI bindings."));
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(new Label("Enabled")); row.Add(new VisualElement { style = { width = 8 } });
                row.Add(DevkitTheme.Button("Open", () => EditorApplication.ExecuteMenuItem("Window/General/Project Settings")));
                el.Add(row);
            }));
        }

    }
}
#endif
