using UnityEngine;
using UnityEngine.EventSystems;

namespace Pitech.XR.Interactables
{
    /// <summary>
    /// Routes EventSystem pointer events back to the owning SelectablesManager.
    /// Auto-attached to each selectable collider's GameObject by SelectablesManager.
    ///
    /// Requires a PhysicsRaycaster component on the scene's active Camera
    /// (typically the ARCamera) and an EventSystem in the scene so that
    /// IPointerDownHandler fires on 3D colliders.
    ///
    /// VR (Meta) path is independent — it still goes through
    /// SelectablesManager.MetaSelect via Meta Event Wrappers.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Pi tech XR/Scenario/Selectable Target")]
    public class SelectableTarget : MonoBehaviour, IPointerDownHandler
    {
        [Tooltip("Manager notified on pointer down. Auto-wired by SelectablesManager; also falls back to an ancestor SelectablesManager.")]
        public SelectablesManager manager;

        Collider _collider;

        void Awake()
        {
            _collider = GetComponent<Collider>();
            if (!manager) manager = GetComponentInParent<SelectablesManager>(true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!manager || !_collider) return;
            manager.HandlePointerDown(_collider);
        }
    }
}
