using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pitech.XR.Interactables
{
    [AddComponentMenu("Pi tech XR/Scenario/Selectables Manager (Meta VR Ready + AR Safe)")]
    public class SelectablesManager : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public Collider collider;      // selectable surface (can be Trigger)
            public GameObject highlight;   // OPTIONAL: toggled when selected
        }

        public enum PlatformMode
        {
            Auto,           // VR if an HMD is active, otherwise Desktop/Mobile
            ForceDesktop,   // always desktop/mobile logic
            ForceVRMeta     // always VR events (for Editor testing)
        }

        [Header("Mode")]
        [Tooltip("Auto: VR (Meta events) when HMD is active, otherwise Desktop/Touch. " +
                 "For AR with Vuforia/ARF, prefer ForceDesktop if Auto mis-detects VR.")]
        public PlatformMode mode = PlatformMode.Auto;

        [Header("Catalog")]
        [Tooltip("Optional: auto-collect colliders from this root at Awake.")]
        public Transform collectRoot;
        public bool autoCollectInChildren = true;
        public LayerMask selectableLayers = ~0;
        public QueryTriggerInteraction triggerHits = QueryTriggerInteraction.Collide;
        public List<Entry> items = new();

        [Header("Desktop/Mobile Picking")]
        [Tooltip("Used only in Desktop/Mobile/AR mode. In VR we rely on Meta events.")]
        public bool pickingEnabled = true;
        [Tooltip("If true, taps/clicks over UI are ignored (EventSystem).")]
        public bool ignoreUI = true;
        [Tooltip("Ray length for screen-point selection.")]
        public float rayLength = 100f;

        [Header("Visuals (fallback)")]
        public bool tintSelected = true;
        public Color tintColor = new Color(1f, 0.85f, 0.1f, 1f);
        public bool useEmission = true;

        public event Action SelectionChanged;

        // state
        readonly HashSet<int> _selected = new();
        readonly Dictionary<int, int> _indexById = new();            // colliderID -> items index
        readonly Dictionary<int, Renderer[]> _renderersById = new();  // colliderID -> renderers

        MaterialPropertyBlock _mpb;
        Camera _cam;

        bool IsVR =>
            mode == PlatformMode.ForceVRMeta ||
            (mode == PlatformMode.Auto && XRPresent());

        static bool XRPresent()
        {
#if UNITY_2020_3_OR_NEWER
            return UnityEngine.XR.XRSettings.isDeviceActive;
#else
            return false;
#endif
        }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _cam = Camera.main;

            if (autoCollectInChildren && collectRoot)
                CollectFromChildren();

            RebuildIndex();
            CacheRenderers();
            SetAll(false); // visuals off at start
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
            RebuildIndex();
            CacheRenderers();
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

                var rs = col.GetComponents<Renderer>();
                if (rs == null || rs.Length == 0) rs = col.GetComponentsInChildren<Renderer>(true);
                _renderersById[col.GetInstanceID()] = rs ?? Array.Empty<Renderer>();
            }
        }

        void Update()
        {
            // In VR we don't read input here; we wait for Meta events.
            if (IsVR) return;
            if (!pickingEnabled) return;

            HandleDesktopOrARInputThisFrame();
        }

        // -------- robust desktop/AR input (Input System & Legacy) --------
        void HandleDesktopOrARInputThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            // 1) Mouse click (editor/desktop)
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                var pos = Mouse.current.position.ReadValue();
                if (!IsOverUI_Mouse())
                    TriggerWithScreenPoint(pos);
            }

            // 2) Touch taps (AR/mobile). Loop ALL touches; primaryTouch can be unreliable.
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    if (!t.press.wasPressedThisFrame) continue;

                    var pos = t.position.ReadValue();
                    var pointerId = t.touchId.ReadValue(); // IMPORTANT for EventSystem UI blocking

                    if (ignoreUI && EventSystem.current != null &&
                        EventSystem.current.IsPointerOverGameObject(pointerId))
                        continue;

                    TriggerWithScreenPoint(pos);
                    // if you want to process only one tap per frame, uncomment next line:
                    // break;
                }
            }
#else
            // Legacy Input: mouse
            if (Input.GetMouseButtonDown(0))
            {
                if (!IsOverUI_Mouse())
                    TriggerWithScreenPoint(Input.mousePosition);
            }

            // Legacy Input: touch
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase != UnityEngine.TouchPhase.Began) continue;

                if (ignoreUI && EventSystem.current != null &&
                    EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    continue;

                TriggerWithScreenPoint(touch.position);
                // break; // if only one tap per frame
            }
#endif
        }

        bool IsOverUI_Mouse()
        {
            if (!ignoreUI || EventSystem.current == null) return false;
            // For mouse, Input System UI module uses the default IsPointerOverGameObject()
            return EventSystem.current.IsPointerOverGameObject();
        }

        void TriggerWithScreenPoint(Vector2 screenPos)
        {
            if (!_cam) _cam = Camera.main;
            if (!_cam) return;

            var ray = _cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            float maxDist = rayLength > 0 ? rayLength : Mathf.Infinity;

            if (Physics.Raycast(ray, out var hit, maxDist, selectableLayers, triggerHits))
            {
                Toggle(hit.collider);
                SelectionChanged?.Invoke();
            }
        }

        // ========================= VR (Meta) API =========================
        /// <summary>Called by Meta Event Wrapper (OnSelect) with the GameObject.</summary>
        public void MetaSelect(GameObject go)
        {
            if (!IsVR || !go) return;
            var col = go.GetComponentInChildren<Collider>();
            if (!col) return;
            Toggle(col);
            SelectionChanged?.Invoke();
        }

        /// <summary>Called by Meta Event Wrapper (OnSelect) with Collider.</summary>
        public void MetaSelectCollider(Collider col)
        {
            if (!IsVR || !col) return;
            Toggle(col);
            SelectionChanged?.Invoke();
        }

        /// <summary>Optional explicit unselect.</summary>
        public void MetaUnselect(GameObject go)
        {
            if (!IsVR || !go) return;
            var col = go.GetComponentInChildren<Collider>();
            if (!col) return;

            if (_indexById.TryGetValue(col.GetInstanceID(), out int idx))
            {
                SetSelected(idx, false);
                SelectionChanged?.Invoke();
            }
        }

        // ====================== Selection & Visuals ======================
        public IReadOnlyCollection<int> SelectedIds => _selected;

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

        public void Toggle(Collider col)
        {
            if (!col) return;
            if (_indexById.TryGetValue(col.GetInstanceID(), out int idx))
                Toggle(idx);
        }

        public void Toggle(int index)
        {
            if (index < 0 || index >= items.Count) return;
            var c = items[index]?.collider;
            if (!c) return;

            int id = c.GetInstanceID();
            bool now = !_selected.Contains(id);
            SetSelected(index, now);
        }

        void SetSelected(int index, bool on)
        {
            var c = items[index]?.collider;
            if (!c) return;

            int id = c.GetInstanceID();
            if (on) _selected.Add(id);
            else _selected.Remove(id);

            SetVisual(index, on);
        }

        void SetVisual(int index, bool on)
        {
            var entry = items[index];
            if (entry == null) return;

            if (entry.highlight) entry.highlight.SetActive(on);

            if (!tintSelected || _mpb == null) return;

            var col = entry.collider;
            if (!col) return;

            if (!_renderersById.TryGetValue(col.GetInstanceID(), out var rs) || rs == null) return;

            foreach (var r in rs)
            {
                if (!r) continue;

                _mpb.Clear();
                if (on)
                {
                    if (r.sharedMaterial && r.sharedMaterial.HasProperty("_BaseColor"))
                        _mpb.SetColor("_BaseColor", tintColor);
                    else
                        _mpb.SetColor("_Color", tintColor);

                    if (useEmission)
                        _mpb.SetColor("_EmissionColor", tintColor);
                }

                r.SetPropertyBlock(_mpb);
            }
        }

        // ============ Optional helper to bind from Meta wrapper ============
        [AddComponentMenu("Pi tech XR/Scenario/Meta Select Relay (optional)")]
        public class MetaSelectRelay : MonoBehaviour
        {
            public SelectablesManager manager;
            public void CallSelect() { if (manager) manager.MetaSelect(gameObject); }
            public void CallUnselect() { if (manager) manager.MetaUnselect(gameObject); }
        }
    }
}
