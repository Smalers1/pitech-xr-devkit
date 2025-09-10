#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public sealed class ToolsPage : IDevkitPage
    {
        public string Title => "Tools";

        public void BuildUI(VisualElement root)
        {
            root.Add(Section("Utilities", el =>
            {
                el.Add(new Label("Project scaffolding, addressables helpers, prefabs, etc. (coming soon)."));
            }));
            {
                var section = DevkitTheme.Section("Scene Categories");
                section.Add(DevkitTheme.Body("Create tidy root groups in the active scene: Lighting, Scene Managers, Environment, Interactables, Timelines, UI, Audio, VFX, Cameras and Debug.", dim: true));

                var actions = DevkitWidgets.Actions(
                    DevkitTheme.Primary("Open", SceneCategoriesWindow.Open)
                );
                section.Add(DevkitTheme.VSpace(6));
                section.Add(actions);
                root.Add(section);
            }
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
    }
}
#endif
