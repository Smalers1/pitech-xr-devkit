using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pitech.XR.Interactables
{
    [AddComponentMenu("Pi tech XR/Scenario/Selectables Manager (Meta VR Ready)")]
    public class SelectablesManager : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public Collider collider;      // selectable surface (μπορεί να είναι Trigger)
            public GameObject highlight;   // OPTIONAL: ενεργοποιείται όταν επιλέγεται
        }

        public enum PlatformMode
        {
            Auto,           // VR αν υπάρχει XR device, αλλιώς Desktop/Mobile
            ForceDesktop,   // πάντα desktop/mobile λογική
            ForceVRMeta     // πάντα VR events (για testing σε Editor)
        }

        [Header("Mode")]
        [Tooltip("Auto: VR (Meta events) όταν υπάρχει HMD, αλλιώς Desktop click.")]
        public PlatformMode mode = PlatformMode.Auto;

        [Header("Catalog")]
        [Tooltip("Αν το δώσεις, θα κάνει auto-collect colliders από κάτω.")]
        public Transform collectRoot;
        public bool autoCollectInChildren = true;
        public LayerMask selectableLayers = ~0;
        public QueryTriggerInteraction triggerHits = QueryTriggerInteraction.Collide;
        public List<Entry> items = new();

        [Header("Desktop/Mobile Picking")]
        public bool pickingEnabled = true;               // ισχύει μόνο για Desktop/Mobile
        public bool ignoreUI = true;                     // αγνοεί UI clicks
        public float rayLength = 100f;

        [Header("Visuals (fallback)")]
        public bool tintSelected = true;
        public Color tintColor = new Color(1f, 0.85f, 0.1f, 1f);
        public bool useEmission = true;

        public event Action SelectionChanged;

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
            // απλός και αρκετά αξιόπιστος τρόπος
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
            SetAll(false); // σβήσε visuals

            // Στο VR ΔΕΝ διαβάζουμε input. Τα events έρχονται από τους Meta Event Wrappers.
            // Στο Desktop/Mobile χρησιμοποιούμε click λογική στο Update().
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
            // Desktop/Mobile μόνο — στο VR περιμένουμε Meta events
            if (IsVR) return;
            if (!pickingEnabled) return;

            if (ignoreUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject())
                return;

            if (!PressedThisFrame()) return;

            if (!_cam) _cam = Camera.main;
            if (!_cam) return;

            Vector2 pos = GetPointerPosition();
            var ray = _cam.ScreenPointToRay(new Vector3(pos.x, pos.y, 0f));

            if (Physics.Raycast(ray, out var hit, rayLength > 0 ? rayLength : Mathf.Infinity, selectableLayers, triggerHits))
            {
                Toggle(hit.collider);
                SelectionChanged?.Invoke();
            }
        }

        Vector2 GetPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            if (Touchscreen.current != null) return Touchscreen.current.primaryTouch.position.ReadValue();
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#else
            return Input.mousePosition;
#endif
        }

        bool PressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0)) return true;
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == UnityEngine.TouchPhase.Began) return true;
            }
#endif
            return false;
        }

        // ========================= VR (Meta) API =========================
        // Τα παρακάτω είναι ΟΛΑ ό,τι χρειάζεται να συνδέσεις στα Event Wrappers.
        // Δεν απαιτούν compile-time references στον Meta SDK.

        /// <summary>
        /// Καλείται από Event Wrapper που δίνει GameObject (π.χ. OnSelect(GameObject)).
        /// </summary>
        public void MetaSelect(GameObject go)
        {
            if (!IsVR || !go) return;
            var col = go.GetComponentInChildren<Collider>();
            if (!col) return;
            Toggle(col);
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Καλείται από Event Wrapper που δίνει Collider (αν το επιτρέπεις στο Inspector).
        /// </summary>
        public void MetaSelectCollider(Collider col)
        {
            if (!IsVR || !col) return;
            Toggle(col);
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Καλείται από Event Wrapper "Unselect" αν θέλεις explicit unselect (optional).
        /// Αν δεν το χρησιμοποιείς, απλώς μένουμε σε toggle-συμπεριφορά.
        /// </summary>
        public void MetaUnselect(GameObject go)
        {
            if (!IsVR || !go) return;
            var col = go.GetComponentInChildren<Collider>();
            if (!col) return;

            if (_indexById.TryGetValue(col.GetInstanceID(), out int idx))
            {
                // επιβάλουμε off
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
                // else _mpb είναι κενό => καθαρίζει overrides

                r.SetPropertyBlock(_mpb);
            }
        }

        // ================= Optional Helper (για εύκολη σύνδεση) =================
        // Βάλε αυτό το component πάνω στο selectable prefab.
        // Στον Meta Interactable Unity Event Wrapper:
        //  - OnSelect  -> MetaSelectRelay.CallSelect()
        //  - OnUnselect(optional) -> MetaSelectRelay.CallUnselect()
        [AddComponentMenu("Pi tech XR/Scenario/Meta Select Relay (optional)")]
        public class MetaSelectRelay : MonoBehaviour
        {
            public SelectablesManager manager;

            // Αν ο Event Wrapper μπορεί να δώσει GameObject, μπορείς να καλέσεις manager.MetaSelect(gameObject) απευθείας.
            // Κρατάμε αυτά τα methods για one-click binding στον Inspector.
            public void CallSelect()
            {
                if (manager) manager.MetaSelect(gameObject);
            }
            public void CallUnselect()
            {
                if (manager) manager.MetaUnselect(gameObject);
            }
        }
    }
}
