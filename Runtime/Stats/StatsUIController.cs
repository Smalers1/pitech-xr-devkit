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
            public TMP_Text text;
            public Slider slider;          // optional
            public string format = "N0";
        }

        public List<Binding> bindings = new();
        readonly Dictionary<StatKey, Binding> map = new();
        readonly Dictionary<StatKey, Coroutine> anims = new();

        StatsRuntime runtime;

        public void Init(StatsRuntime rt, bool syncNow = true)
        {
            runtime = rt;
            map.Clear();
            foreach (var b in bindings)
                if (b != null) map[b.key] = b;

            if (runtime == null) return;

            runtime.OnChanged -= AnimateTo;
            runtime.OnChanged += AnimateTo;

            if (syncNow)
            {
                foreach (var kv in map)
                {
                    var key = kv.Key;
                    var b = kv.Value;

                    // 1) set slider range from config if available
                    if (b.slider && runtime.TryGetRange(key, out var min, out var max))
                    {
                        b.slider.minValue = min;
                        b.slider.maxValue = max;
                    }

                    // 2) push the current value immediately
                    SetImmediate(b, runtime[key]);
                }
            }
        }

        void OnDestroy()
        {
            if (runtime != null) runtime.OnChanged -= AnimateTo;
        }

        void AnimateTo(StatKey k, float from, float to)
        {
            if (!map.TryGetValue(k, out var b)) return;
            if (anims.TryGetValue(k, out var c)) StopCoroutine(c);
            anims[k] = StartCoroutine(Anim(b, from, to));
        }

        System.Collections.IEnumerator Anim(Binding b, float a, float bval)
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
