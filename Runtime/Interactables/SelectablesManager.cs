using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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

        [Header("Catalog")]
        [Tooltip("Where to auto-collect from (optional).")]
        public Transform collectRoot;

        [Tooltip("Auto-collect colliders at Awake from 'collectRoot'.")]
        public bool autoCollectInChildren = true;

        [Tooltip("Layers considered selectable when clicking.")]
        public LayerMask selectableLayers = ~0;

        [Tooltip("How to treat trigger colliders when raycasting.")]
        public QueryTriggerInteraction triggerHits = QueryTriggerInteraction.Collide;

        [Tooltip("All selectable items. No scripts on the objects.")]
        public List<Entry> items = new();

        [Header("Visuals (fallback if no 'highlight' object)")]
        public bool tintSelected = true;
        public Color tintColor = new Color(1f, 0.85f, 0.1f, 1f);
        [Tooltip("If true, also sets _EmissionColor to the tint color (URP/BiRP that support it).")]
        public bool useEmission = true;

        [Header("Runtime")]
        public bool pickingEnabled = true;
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
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;

            if (!PressedThisFrame()) return;

            if (!_cam) _cam = Camera.main;
            var ray = _cam ? _cam.ScreenPointToRay(Input.mousePosition) : default;

            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, selectableLayers, triggerHits))
            {
                var id = hit.collider.GetInstanceID();
                if (_indexById.TryGetValue(id, out int idx))
                {
                    Toggle(idx);
                    SelectionChanged?.Invoke();
                }
            }
        }

        static bool PressedThisFrame()
        {
            if (Input.GetMouseButtonDown(0)) return true;
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
            return false;
        }

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
                    // Try common color properties
                    if (r.sharedMaterial && r.sharedMaterial.HasProperty("_BaseColor"))
                        _mpb.SetColor("_BaseColor", tintColor);     // URP Lit
                    else
                        _mpb.SetColor("_Color", tintColor);          // Standard/BiRP

                    if (useEmission)
                    {
                        _mpb.SetColor("_EmissionColor", tintColor);
                        // Some shaders gate emission by keyword; MPB can still push the color for URP
                    }
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
