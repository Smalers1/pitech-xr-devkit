// Packages/com.pitech.xr.devkit/Editor/Core.Editor/Pages/SettingsPage.cs
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor.Pages
{
    internal sealed class SettingsPage : IDevkitPage
    {
        public string Title => "Settings";

        public void Build(VisualElement root)
        {
            root.Clear();

            root.Add(new Label("Settings")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    marginBottom = 8
                }
            });

            root.Add(new Label("Project setup helpers will end up here (folders, layers, tags, URP, etc.)."));
        }
    }
}
