using System;
using UnityEngine;

namespace Pitech.XR.Interactables
{
    /// <summary>
    /// Lightweight grabbable marker and event hub.
    /// Works standalone (physics grab) and is extended automatically by the
    /// "Pi tech > Make Grabbable" editor wizard when Meta or Fusion SDKs are present.
    /// </summary>
    [AddComponentMenu("Pi tech XR/Interactables/Grabbable")]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class Grabbable : MonoBehaviour
    {
        // ───────── Settings ─────────

        [Header("Grab Behaviour")]
        [Tooltip("Allow two-handed grab (if the VR SDK supports it).")]
        public bool allowTwoHandedGrab;

        [Tooltip("Snap back to the original position/rotation when released.")]
        public bool snapBackOnRelease;

        [Tooltip("When true, physics (gravity) is disabled while grabbed.")]
        public bool kinematicWhileGrabbed = true;

        [Header("Constraints")]
        [Tooltip("Optional: restrict movement to the local XZ plane (useful for sliders/drawers).")]
        public bool lockYAxis;

        // ───────── Runtime State ─────────

        /// <summary>True while any hand/controller is holding this object.</summary>
        public bool IsGrabbed { get; private set; }

        /// <summary>The Transform currently grabbing this object (null when not grabbed).</summary>
        public Transform GrabbedBy { get; private set; }

        /// <summary>Raised when any grabber picks this object up.</summary>
        public event Action<Grabbable> OnGrabbed;

        /// <summary>Raised when the object is released.</summary>
        public event Action<Grabbable> OnReleased;

        // ───────── Snap-back state ─────────
        Vector3 _startPos;
        Quaternion _startRot;
        Rigidbody _rb;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _startPos = transform.localPosition;
            _startRot = transform.localRotation;
        }

        // ───────── Public API (called by VR SDK wrappers or physics code) ─────────

        /// <summary>Call when a grabber picks this object up.</summary>
        public void NotifyGrabbed(Transform grabber = null)
        {
            if (IsGrabbed) return;
            IsGrabbed = true;
            GrabbedBy = grabber;

            if (kinematicWhileGrabbed && _rb) _rb.isKinematic = true;

            OnGrabbed?.Invoke(this);
        }

        /// <summary>Call when the grabber releases this object.</summary>
        public void NotifyReleased()
        {
            if (!IsGrabbed) return;
            IsGrabbed = false;
            GrabbedBy = null;

            if (kinematicWhileGrabbed && _rb) _rb.isKinematic = false;

            if (snapBackOnRelease)
            {
                transform.localPosition = _startPos;
                transform.localRotation = _startRot;
                if (_rb)
                {
                    _rb.velocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
            }

            OnReleased?.Invoke(this);
        }
    }
}
