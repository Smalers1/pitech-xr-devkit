using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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
        [Tooltip("Auto: VR (Meta events) when HMD is active, otherwise Desktop/Touch.")]
        public PlatformMode mode = PlatformMode.Auto;

        [Header("Platform Safeguards")]
        [Tooltip("On Android/iOS always use EventSystem pointer picking, never VR events.")]
        public bool forceDesktopOnMobile = true;

        [Tooltip("Treat XR as VR only if Meta Interactors exist in the scene.")]
        public bool xrIsVrOnlyIfMetaPresent = true;

        [Header("Catalog")]
        [Tooltip("Optional: auto-collect colliders from this root at Awake.")]
        public Transform collectRoot;
        public bool autoCollectInChildren = true;
        public LayerMask selectableLayers = ~0;
        public List<Entry> items = new();

        [Header("Runtime Gating")]
        [Tooltip("When false, HandlePointerDown ignores events. Used by SelectionLists / SceneManager to enable picking only while a question is active.")]
        public bool pickingEnabled = true;

        [Header("Visuals (fallback)")]
        public bool tintSelected = true;
        public Color tintColor = new Color(1f, 0.85f, 0.1f, 1f);
        public bool useEmission = true;

        public event Action SelectionChanged;

        // state
        readonly HashSet<int> _selected = new();
        readonly Dictionary<int, int> _indexById = new();            // colliderID -> items index
        readonly Dictionary<int, Renderer[]> _renderersById = new(); // colliderID -> renderers

        MaterialPropertyBlock _mpb;

        bool IsVR
        {
            get
            {
                if (mode == PlatformMode.ForceVRMeta) return true;
                if (mode == PlatformMode.ForceDesktop) return false;

                if (forceDesktopOnMobile && Application.isMobilePlatform) return false;

#if UNITY_2020_3_OR_NEWER
                bool xrActive = UnityEngine.XR.XRSettings.isDeviceActive;
#else
                bool xrActive = false;
#endif
                if (!xrActive) return false;

                if (xrIsVrOnlyIfMetaPresent && !MetaStuffPresent()) return false;
                return true;
            }
        }

        static bool MetaStuffPresent()
        {
            return HasTypeInScene("RayInteractor")
                || HasTypeInScene("InteractableUnityEventWrapper");
        }
        static bool HasTypeInScene(string shortTypeName)
        {
            var all = Resources.FindObjectsOfTypeAll<Component>();
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i]?.GetType();
                if (t != null && t.Name == shortTypeName) return true;
            }
            return false;
        }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();

            if (autoCollectInChildren && collectRoot)
                CollectFromChildren();

            RebuildIndex();
            CacheRenderers();
            EnsureSelectableTargets();
            SetAll(false); // visuals off at start
        }

        void Start()
        {
            if (IsVR) return;

            // Runtime sanity check: the desktop/AR path relies on EventSystem +
            // a PhysicsRaycaster (typically on the ARCamera). Warn rather than
            // silently fail when either is missing, so integration bugs are
            // caught at scene load instead of reported as "interactions broken".
            if (EventSystem.current == null)
            {
                Debug.LogWarning(
                    "[SelectablesManager] No EventSystem in the loaded scenes. " +
                    "Selectable colliders will not receive pointer events.", this);
            }

#if UNITY_2023_1_OR_NEWER
            var raycaster = UnityEngine.Object.FindFirstObjectByType<PhysicsRaycaster>();
#else
            var raycaster = UnityEngine.Object.FindObjectOfType<PhysicsRaycaster>();
#endif
            if (raycaster == null)
            {
                Debug.LogWarning(
                    "[SelectablesManager] No PhysicsRaycaster found in the scene. " +
                    "Add one to your ARCamera (or main Camera) — IPointerDownHandler " +
                    "will not dispatch to 3D colliders without it.", this);
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
            RebuildIndex();
            CacheRenderers();
            EnsureSelectableTargets();
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

        /// <summary>
        /// Ensure every selectable collider has a SelectableTarget component
        /// so EventSystem can route IPointerDownHandler back to this manager.
        /// Idempotent: safe to call multiple times (Awake + CollectFromChildren).
        /// </summary>
        void EnsureSelectableTargets()
        {
            for (int i = 0; i < items.Count; i++)
            {
                var col = items[i]?.collider;
                if (!col) continue;

                var go = col.gameObject;
                var target = go.GetComponent<SelectableTarget>();
                if (!target) target = go.AddComponent<SelectableTarget>();
                target.manager = this;
            }
        }

        // ===================== Desktop/Mobile/AR entry =====================

        /// <summary>
        /// Called by SelectableTarget.OnPointerDown. EventSystem + PhysicsRaycaster
        /// has already resolved the correct collider and accounts for UI blocking
        /// via GraphicRaycaster ordering.
        /// </summary>
        public void HandlePointerDown(Collider col)
        {
            if (!pickingEnabled) return; // gated by SelectionLists / SceneManager during quiz flow
            if (IsVR) return; // VR uses MetaSelect instead
            if (!col) return;
            Toggle(col);
            SelectionChanged?.Invoke();
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

        public void Toggle(int index)
        {
            if (index < 0 || index >= items.Count) return;
            var c = items[index]?.collider;
            if (!c) return;

            int id = c.GetInstanceID();
            bool now = !_selected.Contains(id);
            SetSelected(index, now);
        }

        public void Toggle(Collider col)
        {
            if (!col) return;
            if (_indexById.TryGetValue(col.GetInstanceID(), out int idx))
                Toggle(idx);
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
