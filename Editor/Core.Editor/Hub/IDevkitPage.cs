// Packages/com.pitech.xr.devkit/Editor/Core.Editor/Pages/IDevkitPage.cs
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor.Pages
{
    internal interface IDevkitPage
    {
        string Title { get; }
        void Build(VisualElement root);
    }
}
