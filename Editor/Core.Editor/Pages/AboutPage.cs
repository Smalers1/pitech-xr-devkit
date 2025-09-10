#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public sealed class AboutPage : IDevkitPage
    {
        public string Title => "About";

        public void BuildUI(VisualElement root)
        {
            root.Add(DevkitTheme.Section("Pi tech XR DevKit", el =>
            {
                if (DevkitContext.SidebarLogo != null)
                {
                    var img = new Image { image = DevkitContext.SidebarLogo };
                    img.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                    img.style.height = 64;
                    img.style.marginBottom = 6;
                    el.Add(img);
                }
                el.Add(new Label($"Version: {DevkitContext.Version}"));
                el.Add(new Label(" Pi tech"));
            }));
        }
    }
}
#endif
