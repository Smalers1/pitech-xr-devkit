using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using Pitech.XR.Stats;

namespace Pitech.XR.Scenario
{
    public class SceneManager : MonoBehaviour
    {
        [Header("Scenario")]
        public Scenario scenario;

        [Header("Stats")]
        public StatsConfig statsConfig;
        public StatsUIController statsUI;

        [Header("Start")]
        public bool autoStart = true;

        public int StepIndex { get; private set; } = -1;
        public StatsRuntime Runtime { get; private set; } = new();

        void Awake()
        {
            Runtime.Reset(statsConfig);
            if (statsUI) statsUI.Init(Runtime);
        }

        void Start()
        {
            if (autoStart) StartCoroutine(Run());
        }

        public void Restart()
        {
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (!scenario || scenario.steps == null) yield break;

            for (int i = 0; i < scenario.steps.Count; i++)
            {
                StepIndex = i;
                var s = scenario.steps[i];
                if (s == null) continue;

                if (s is TimelineStep tl) yield return RunTimeline(tl);
                else if (s is CueCardsStep cc) yield return RunCueCards(cc);
                else if (s is QuestionStep q) yield return RunQuestion(q);
            }
        }

        IEnumerator RunTimeline(TimelineStep s)
        {
            var d = s.director;
            if (!d) yield break;

            if (s.rewindOnEnter) d.time = 0;
            d.Play();

            if (s.waitForEnd)
                while (d.state == PlayState.Playing) yield return null;
        }

        IEnumerator RunCueCards(CueCardsStep s)
        {
            int count = s.cards != null ? s.cards.Length : 0;
            if (count == 0) yield break;

            var fade = s.fadeCurve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
            var scale = s.scaleCurve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);

            for (int i = 0; i < count; i++)
            {
                var go = s.cards[i];
                if (!go) continue;
                var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
                cg.alpha = 0;
                go.SetActive(false);
                var rt = go.GetComponent<RectTransform>();
                if (rt) rt.localScale = Vector3.one;
            }

            SetVisible(s.extraObject, false, s.useRenderersForExtra);
            if (s.tapHint) s.tapHint.SetActive(false);

            var d = s.director;
            if (d && d.state != PlayState.Playing) d.Play();
            double prevTime = d ? d.time : 0.0f;

            int cur = -1;
            bool animating = false;
            float curElapsed = 0f;

            float TimeFor(int idx)
            {
                if (s.cueTimes == null || s.cueTimes.Length == 0) return 9999f;
                if (s.cueTimes.Length == 1) return Mathf.Max(0f, s.cueTimes[0]);
                if (idx >= 0 && idx < s.cueTimes.Length) return Mathf.Max(0f, s.cueTimes[idx]);
                return s.cueTimes[^1];
            }

            IEnumerator FadeIn(int idx)
            {
                animating = true;
                var go = s.cards[idx];
                if (!go) { animating = false; yield break; }

                var cg = go.GetComponent<CanvasGroup>();
                var rt = go.GetComponent<RectTransform>();
                go.SetActive(true);
                go.transform.SetAsLastSibling();
                cg.alpha = 0;

                Vector3 startScale = Vector3.one * (s.popScale > 1f ? 1f / s.popScale : 1f);
                if (rt) rt.localScale = startScale;

                float t = 0, dur = Mathf.Max(0.0001f, s.fadeDuration);
                while (t < dur)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / dur);
                    cg.alpha = fade.Evaluate(k);

                    if (rt && s.popScale > 1f)
                    {
                        float ks = Mathf.Clamp01(t / Mathf.Max(0.0001f, s.popDuration));
                        float sc = Mathf.Lerp(startScale.x, 1f, scale.Evaluate(ks));
                        rt.localScale = new Vector3(sc, sc, 1f);
                    }
                    yield return null;
                }
                cg.alpha = 1f; if (rt) rt.localScale = Vector3.one;

                if (s.extraObject && idx == s.extraShowAtIndex)
                    SetVisible(s.extraObject, true, s.useRenderersForExtra);

                animating = false;
            }

            IEnumerator FadeOut(int idx)
            {
                animating = true;
                var go = idx >= 0 && idx < count ? s.cards[idx] : null;
                if (go)
                {
                    var cg = go.GetComponent<CanvasGroup>();
                    float t = 0, dur = Mathf.Max(0.0001f, s.fadeDuration);
                    float start = cg.alpha;
                    while (t < dur)
                    {
                        t += Time.deltaTime;
                        float k = Mathf.Clamp01(t / dur);
                        cg.alpha = Mathf.Lerp(start, 0f, fade.Evaluate(k));
                        yield return null;
                    }
                    cg.alpha = 0f;
                    go.SetActive(false);
                }
                animating = false;
            }

            if (s.autoShowFirst)
            {
                cur = 0;
                yield return FadeIn(cur);
                if (s.tapHint) s.tapHint.SetActive(true);
            }

            while (true)
            {
                float dt;
                if (d)
                {
                    double now = d.time;
                    dt = Mathf.Max(0f, (float)(now - prevTime));
                    prevTime = now;
                }
                else dt = Time.deltaTime;

                curElapsed += dt;

                bool tap = GetTap() && !animating;
                bool timeUp = cur >= 0 && curElapsed >= TimeFor(cur);

                if ((tap || timeUp) && !animating)
                {
                    if (cur >= count - 1)
                    {
                        if (s.tapHint) s.tapHint.SetActive(false);
                        yield return FadeOut(cur);

                        if (s.extraObject && s.hideExtraWithFinalTap)
                            SetVisible(s.extraObject, false, s.useRenderersForExtra);
                        break;
                    }

                    if (s.tapHint) s.tapHint.SetActive(false);
                    yield return FadeOut(cur);
                    cur++;
                    curElapsed = 0f;
                    yield return FadeIn(cur);
                    if (s.tapHint) s.tapHint.SetActive(true);
                }

                yield return null;
            }
        }

        static bool GetTap()
        {
            if (Input.GetMouseButtonDown(0)) return true;
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
            return false;
        }

        static void SetVisible(GameObject go, bool visible, bool byRenderers)
        {
            if (!go) return;
            if (byRenderers)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
                var cg = go.GetComponentInChildren<CanvasGroup>(true);
                if (cg) cg.alpha = visible ? 1f : 0f;
            }
            else go.SetActive(visible);
        }

        IEnumerator RunQuestion(QuestionStep q)
        {
            Choice picked = null;
            System.Action cleanup = () => { };
            foreach (var c in q.choices)
            {
                if (!c.button) continue;
                UnityEngine.Events.UnityAction h = () => { picked = c; };
                c.button.onClick.AddListener(h);
                cleanup += () => c.button.onClick.RemoveListener(h);
            }

            if (q.panelRoot) q.panelRoot.gameObject.SetActive(true);
            if (q.panelAnimator && !string.IsNullOrEmpty(q.showTrigger)) q.panelAnimator.SetTrigger(q.showTrigger);

            while (picked == null) yield return null;

            foreach (var eff in picked.effects)
            {
                float cur = Runtime[eff.key];
                float next = Mathf.Clamp(eff.Apply(cur),
                    statsConfig.GetRange(eff.key).x,
                    statsConfig.GetRange(eff.key).y);
                Runtime[eff.key] = next;
            }

            if (q.panelAnimator && !string.IsNullOrEmpty(q.hideTrigger))
                q.panelAnimator.SetTrigger(q.hideTrigger);
            else
                yield return new WaitForSeconds(q.fallbackHideSeconds);

            if (q.panelRoot) q.panelRoot.gameObject.SetActive(false);
            cleanup();
        }
    }
}
