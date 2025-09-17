using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Pitech.XR.Stats
{
    public class StatsUIController : MonoBehaviour
    {
        [System.Serializable]
        public class Binding
        {
            public StatKey key;
            public TMP_Text text;   // optional
            public Slider slider;  // optional
            public string format = "N0";
        }

        [Tooltip("(Editor-only) If assigned, ranges are pulled from this config on validate.")]
        [SerializeField] StatsConfig editorConfig;

        public List<Binding> bindings = new();

        readonly Dictionary<StatKey, Binding> map = new();
        readonly Dictionary<StatKey, Coroutine> anims = new();

        StatsRuntime runtime;

        /// Call from SceneManager. syncNow=true paints current values immediately.
        public void Init(StatsRuntime rt, bool syncNow = true)
        {
            runtime = rt;

            map.Clear();
            foreach (var b in bindings)
                if (b != null) map[b.key] = b;

            if (runtime != null)
            {
                runtime.OnChanged -= AnimateTo;
                runtime.OnChanged += AnimateTo;

                if (syncNow)
                {
                    foreach (var kv in map)
                    {
                        var b = kv.Value;
                        float v = 0f;
                        try { v = runtime[kv.Key]; } catch { /* seeded by SceneManager */ }
                        SetImmediate(b, v);
                    }
                }
            }
        }

        /// Push min/max from config into any bound sliders.
        public void ApplyConfig(StatsConfig cfg, bool alsoSetDefaultsToUI = false)
        {
            if (cfg == null) return;

            foreach (var b in bindings)
            {
                if (b == null || b.slider == null) continue;

                var range = cfg.GetRange(b.key); // (min,max)
                b.slider.minValue = range.x;
                b.slider.maxValue = range.y;

                if (alsoSetDefaultsToUI)
                    SetImmediate(b, cfg.GetDefault(b.key));
            }
        }

        void OnDestroy()
        {
            if (runtime != null) runtime.OnChanged -= AnimateTo;
        }

#if UNITY_EDITOR
        // Editor convenience: when you assign Editor Config, mirror ranges/defaults in the inspector.
        void OnValidate()
        {
            if (editorConfig != null && !Application.isPlaying)
                ApplyConfig(editorConfig, alsoSetDefaultsToUI: true);
        }
#endif

        void AnimateTo(StatKey k, float from, float to)
        {
            if (!map.TryGetValue(k, out var b)) return;
            if (anims.TryGetValue(k, out var c)) StopCoroutine(c);
            anims[k] = StartCoroutine(Anim(b, from, to));
        }

        IEnumerator Anim(Binding b, float a, float bval)
        {
            float t = 0f, dur = 0.5f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, dur);
                float x = Mathf.Lerp(a, bval, Mathf.SmoothStep(0, 1, t));
                UpdateVisuals(b, x);
                yield return null;
            }
            SetImmediate(b, bval);
        }

        void SetImmediate(Binding b, float value) => UpdateVisuals(b, value);

        void UpdateVisuals(Binding b, float value)
        {
            if (b.text) b.text.text = value.ToString(string.IsNullOrEmpty(b.format) ? "N0" : b.format);
            if (b.slider) b.slider.value = Mathf.Clamp(value, b.slider.minValue, b.slider.maxValue);
        }
    }
}
