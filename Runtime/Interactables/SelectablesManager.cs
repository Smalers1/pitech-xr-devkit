using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;                 // Mouse, Touchscreen, Gamepad, etc.
using UnityEngine.InputSystem.UI;             // (optional, not required)
#endif

namespace Pitech.XR.Interactables
{
    [AddComponentMenu("Pi tech XR/Scenario/Selectables Manager")]
    public class SelectablesManager : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            public Collider collider;         // selectable surface (can be a Trigger)
            public GameObject highlight;      // OPTIONAL: toggled when selected
        }

        public enum RaycastSource
        {
            CameraScreenPoint,    // mobile/desktop (mouse/touch)
            TransformRay,         // VR controller or head/gaze (forward ray)
            External              // you call TriggerWithRay from your own input code
        }

        [Header("Catalog")]
        [Tooltip("Where to auto-collect from (optional).")]
        public Transform collectRoot;

        [Tooltip("Auto-collect colliders at Awake from 'collectRoot'.")]
        public bool autoCollectInChildren = true;

        [Tooltip("Layers considered selectable when raycasting.")]
        public LayerMask selectableLayers = ~0;

        [Tooltip("How to treat trigger colliders when raycasting.")]
        public QueryTriggerInteraction triggerHits = QueryTriggerInteraction.Collide;

        [Tooltip("All selectable items. No scripts on the objects.")]
        public List<Entry> items = new();

        [Header("Picking")]
        public bool pickingEnabled = true;

        [Tooltip("How to build the ray used for selection.")]
        public RaycastSource raySource = RaycastSource.CameraScreenPoint;

        [Tooltip("Transform used when Ray Source = TransformRay (e.g., VR controller).")]
        public Transform rayTransform;

        [Tooltip("Max ray distance when using TransformRay/External.")]
        public float rayLength = 100f;

#if ENABLE_INPUT_SYSTEM
        [Header("Input (New Input System)")]
        [Tooltip("Optional: action that represents 'Select/Click'. If not set, we fall back to Mouse/Touch/Gamepad checks.")]
        public InputActionReference selectAction;
#endif

        [Header("UI Blocking")]
        [Tooltip("If true, clicks over UI are ignored (EventSystem).")]
        public bool ignoreUI = true;

        [Header("Visuals (fallback if no 'highlight' object)")]
        public bool tintSelected = true;
        public Color tintColor = new Color(1f, 0.85f, 0.1f, 1f);
        [Tooltip("If true, also sets _EmissionColor to the tint color (URP/BiRP that support it).")]
        public bool useEmission = true;

        public event System.Action SelectionChanged;

        // state
        readonly HashSet<int> _selected = new();
        readonly Dictionary<int, int> _indexById = new();              // colliderID -> items index
        readonly Dictionary<int, Renderer[]> _renderersById = new();    // colliderID -> renderers

        MaterialPropertyBlock _mpb;   // created in Awake
        Camera _cam;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _cam = Camera.main;

            if (autoCollectInChildren && collectRoot)
                CollectFromChildren();

            RebuildIndex();
            CacheRenderers();
            SetAll(false); // ensure visuals off

#if ENABLE_INPUT_SYSTEM
            // Enable action if provided (for play-in-editor convenience)
            if (selectAction != null && selectAction.action != null && !selectAction.action.enabled)
                selectAction.action.Enable();
#endif
        }

        void OnDestroy()
        {
#if ENABLE_INPUT_SYSTEM
            if (selectAction != null && selectAction.action != null && selectAction.action.enabled)
                selectAction.action.Disable();
#endif
        }

        void RebuildIndex()
        {
            _indexById.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                var c = items[i]?.collider;
                if (c) _indexById[c.GetInstanceID()] = i;
            }
        }

        void CacheRenderers()
        {
            _renderersById.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                var col = items[i]?.collider;
                if (!col) continue;

                // first try same GO, then children
                var rs = col.GetComponents<Renderer>();
                if (rs == null || rs.Length == 0) rs = col.GetComponentsInChildren<Renderer>(true);
                _renderersById[col.GetInstanceID()] = rs ?? System.Array.Empty<Renderer>();
            }
        }

        public void CollectFromChildren()
        {
            if (!collectRoot) return;
            items.Clear();
            var cols = collectRoot.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                if (((1 << c.gameObject.layer) & selectableLayers) == 0) continue;
                items.Add(new Entry { collider = c, highlight = null });
            }
        }

        void Update()
        {
            if (!pickingEnabled) return;

            // ignore clicks on UI
            if (ignoreUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject())
                return;

            // If the source is External, we don't read input here.
            if (raySource == RaycastSource.External)
                return;

            if (!PressedThisFrame()) return;

            Ray ray;
            if (!BuildRay(out ray)) return;

            TrySelectFromRay(ray);
        }

        // -------- external use (XR line / custom pointer) ----------
        /// <summary>Trigger a selection check from an external ray (e.g., XR controller line) when your own input fires.</summary>
        public void TriggerWithRay(Ray ray)
        {
            if (!pickingEnabled) return;
            TrySelectFromRay(ray);
        }

        // -------- core picking logic ----------
        void TrySelectFromRay(Ray ray)
        {
            if (Physics.Raycast(ray, out var hit, rayLength > 0 ? rayLength : Mathf.Infinity, selectableLayers, triggerHits))
            {
                var id = hit.collider.GetInstanceID();
                if (_indexById.TryGetValue(id, out int idx))
                {
                    Toggle(idx);
                    SelectionChanged?.Invoke();
                }
            }
        }

        bool BuildRay(out Ray ray)
        {
            ray = default;

            switch (raySource)
            {
                case RaycastSource.CameraScreenPoint:
                    {
                        if (!_cam) _cam = Camera.main;
                        if (!_cam) return false;

                        Vector2 pos = GetPointerPosition();
                        ray = _cam.ScreenPointToRay(new Vector3(pos.x, pos.y, 0f));
                        return true;
                    }

                case RaycastSource.TransformRay:
                    {
                        Transform t = rayTransform ? rayTransform : (_cam ? _cam.transform : null);
                        if (!t) return false;
                        ray = new Ray(t.position, t.forward);
                        return true;
                    }
            }

            return false;
        }

        Vector2 GetPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            if (Touchscreen.current != null) return Touchscreen.current.primaryTouch.position.ReadValue();
            var gp = Gamepad.current;
            if (gp != null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f); // center fallback
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#else
            return Input.mousePosition;
#endif
        }

        bool PressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
    // 1) Preferred: explicit action (works for OpenXR, Quest, Desktop).
    if (selectAction != null && selectAction.action != null && selectAction.action.triggered)
        return true;

    // 2) Desktop/mobile fallbacks.
    if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
    if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;

    // 3) XR OpenXR fallback: read right/left controller triggerPressed
    var r = UnityEngine.InputSystem.XR.XRController.rightHand;
    if (r != null)
    {
        var tPressed = r.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("triggerPressed");
        if (tPressed != null && tPressed.wasPressedThisFrame) return true;
    }
    var l = UnityEngine.InputSystem.XR.XRController.leftHand;
    if (l != null)
    {
        var tPressed = l.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("triggerPressed");
        if (tPressed != null && tPressed.wasPressedThisFrame) return true;
    }

    // Optional: gamepad
    var gp = Gamepad.current;
    if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.leftStickButton.wasPressedThisFrame))
        return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0)) return true;
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == UnityEngine.TouchPhase.Began) return true;
#endif

            return false;
        }


        // --------------- selection state & visuals ----------------
        public void ClearAll(bool alsoTurnOffHighlights)
        {
            _selected.Clear();
            if (alsoTurnOffHighlights) SetAll(false);
        }

        public void SetAll(bool on)
        {
            for (int i = 0; i < items.Count; i++)
                SetVisual(i, on && items[i]?.collider);
            if (!on) _selected.Clear();
        }

        public IReadOnlyCollection<int> SelectedIds => _selected;

        public void Toggle(int index)
        {
            if (index < 0 || index >= items.Count) return;
            var c = items[index]?.collider;
            if (!c) return;

            int id = c.GetInstanceID();
            bool now = !_selected.Contains(id);

            if (now) _selected.Add(id);
            else _selected.Remove(id);

            SetVisual(index, now);
        }

        public void Toggle(Collider col)
        {
            if (!col) return;
            if (_indexById.TryGetValue(col.GetInstanceID(), out int idx))
                Toggle(idx);
        }

        void SetVisual(int index, bool on)
        {
            var entry = items[index];
            if (entry == null) return;

            // 1) optional explicit highlight
            if (entry.highlight) entry.highlight.SetActive(on);

            // 2) fallback tint
            if (!tintSelected || _mpb == null) return;

            var col = entry.collider;
            if (!col) return;

            if (!_renderersById.TryGetValue(col.GetInstanceID(), out var rs) || rs == null) return;

            foreach (var r in rs)
            {
                if (!r) continue;

                if (on)
                {
                    _mpb.Clear();
                    if (r.sharedMaterial && r.sharedMaterial.HasProperty("_BaseColor"))
                        _mpb.SetColor("_BaseColor", tintColor);     // URP Lit
                    else
                        _mpb.SetColor("_Color", tintColor);          // Standard/BiRP

                    if (useEmission)
                        _mpb.SetColor("_EmissionColor", tintColor);
                }
                else
                {
                    _mpb.Clear(); // remove overrides -> back to original
                }

                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
