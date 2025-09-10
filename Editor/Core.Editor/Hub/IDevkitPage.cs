#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public interface IDevkitPage
    {
        string Title { get; }
        /// <summary>Build the page UI under the given root.</summary>
        void BuildUI(VisualElement root);
    }
}
#endif
