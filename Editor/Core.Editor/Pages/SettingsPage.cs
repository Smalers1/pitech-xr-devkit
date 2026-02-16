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
            root.Add(Section("DevKit", el =>
            {
                el.Add(new Label($"Version: {DevkitContext.Version}"));
                el.Add(new Label($"Timeline present: {DevkitContext.HasTimeline}"));
                el.Add(new Label($"TextMeshPro present: {DevkitContext.HasTextMeshPro}"));
                el.Add(new Label($"Addressables present: {DevkitContext.HasAddressables}"));
                el.Add(new Label($"CCD package present: {DevkitContext.HasCcdManagement}"));
            }));

            root.Add(Section("Project Settings", el =>
            {
                el.Add(Button("Open Project Settings", () => SettingsService.OpenProjectSettings("Project")));
            }));
        }

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
