// Packages/com.pitech.xr.devkit/Editor/Core.Editor/Pages/ModulesPage.cs
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor.Pages
{
    internal sealed class ModulesPage : IDevkitPage
    {
        public string Title => "Modules";

        public void Build(VisualElement root)
        {
            root.Clear();

            root.Add(new Label("Modules")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    marginBottom = 8
                }
            });

            root.Add(new Label("List installed/available modules here. Each row can have enable/disable toggles, quick actions, etc."));
        }
    }
}
