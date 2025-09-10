#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public sealed class SettingsPage : IDevkitPage
    {
        public string Title => "Settings";

        public void BuildUI(VisualElement root)
        {
            root.Add(DevkitTheme.Section("DevKit", el =>
            {
                el.Add(new Label($"Version: {DevkitContext.Version}"));
                el.Add(new Label($"Timeline present: {DevkitContext.HasTimeline}"));
                el.Add(new Label($"TextMeshPro present: {DevkitContext.HasTextMeshPro}"));
            }));

            root.Add(DevkitTheme.Section("Project Settings", el =>
            {
                el.Add(DevkitTheme.Button("Open Project Settings", () => SettingsService.OpenProjectSettings("Project")));
            }));
        }
    }
}
#endif
