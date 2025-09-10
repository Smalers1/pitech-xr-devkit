// Packages/com.pitech.xr.devkit/Editor/Core.Editor/Pages/ToolsPage.cs
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor.Pages
{
    internal sealed class ToolsPage : IDevkitPage
    {
        public string Title => "Tools";

        public void Build(VisualElement root)
        {
            root.Clear();

            root.Add(new Label("Tools")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    marginBottom = 8
                }
            });

            root.Add(new Label("Add utilities like addressables helpers, scene templates, networking setup, etc."));
        }
    }
}
