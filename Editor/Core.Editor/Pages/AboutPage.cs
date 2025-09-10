using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor.Pages
{
    internal sealed class AboutPage : IDevkitPage
    {
        public string Title => "About";

        public void Build(VisualElement root)
        {
            root.Clear();

            root.Add(new Label("About Pi tech XR DevKit")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    marginBottom = 8
                }
            });

            root.Add(new Label("A modular, in-house toolkit for building XR apps faster on Unity LTS.\n" +
                               "This page is a placeholder; we can add credits, links, version info, etc."));
        }
    }
}
