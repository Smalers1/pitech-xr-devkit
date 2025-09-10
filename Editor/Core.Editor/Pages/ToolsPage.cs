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
            root.Add(DevkitTheme.Section("Utilities", el =>
            {
                el.Add(new Label("Project scaffolding, addressables helpers, prefabs, etc. (coming soon)."));
            }));
        }

    }
}
#endif
