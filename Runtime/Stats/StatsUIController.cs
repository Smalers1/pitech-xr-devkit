using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT
using TMPro;
#endif

namespace Pitech.XR.Stats
{
    public class StatsUIController : MonoBehaviour
    {
        [System.Serializable]
        public class Binding
        {
            public StatKey key;
#if TMP_PRESENT
            public TMP_Text text;
#else
            public Text text;
#endif
            public Slider slider;          // optional
            public float sliderMax = 100f; // optional
            public string format = "N0";
        }

        public List<Binding> bindings = new();
        Dictionary<StatKey, Binding> map = new();
        Dictionary<StatKey, Coroutine> anims = new();

        public void Init(StatsRuntime rt)
        {
            map.Clear();
            foreach (var b in bindings) if (b != null) map[b.key] = b;
            rt.OnChanged += AnimateTo;
        }

        void AnimateTo(StatKey k, float from, float to)
        {
            if (!map.TryGetValue(k, out var b)) return;
            if (anims.TryGetValue(k, out var c)) StopCoroutine(c);
            anims[k] = StartCoroutine(Anim(b, from, to));
        }

        IEnumerator Anim(Binding b, float a, float bval)
        {
            float t = 0, dur = 0.5f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float x = Mathf.Lerp(a, bval, Mathf.SmoothStep(0, 1, t));
                if (b.text) b.text.text = x.ToString(b.format);
                if (b.slider) b.slider.value = Mathf.Clamp01(x / Mathf.Max(0.0001f, b.sliderMax));
                yield return null;
            }
            if (b.text) b.text.text = bval.ToString(b.format);
            if (b.slider) b.slider.value = Mathf.Clamp01(bval / Mathf.Max(0.0001f, b.sliderMax));
        }
    }
}
