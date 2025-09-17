using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.UI;
using Pitech.XR.Stats;

namespace Pitech.XR.Scenario
{
    [AddComponentMenu("Pi tech XR/Scenario/Scene Manager")]
    public class SceneManager : MonoBehaviour
    {
        [Header("Scenario")]
        public Scenario scenario;
        public bool autoStart = true;

        public StatsUIController statsUI;
        public StatsConfig statsConfig;
        bool _statsBound;

        [Header("Stats (optional)")]
        public StatsRuntime runtime;   // assign if you have one. if null we create a plain instance

        /// Current step index while running. -1 when idle or finished
        public int StepIndex { get; private set; } = -1;

        Coroutine _run;
        readonly List<(Button btn, UnityAction fn)> _wired = new();
        string _nextGuidFromChoice;

        void Awake()
        {
            // Only set up stats if the feature is present (UI or config).
            // Only if feature present
            if (statsUI != null || statsConfig != null)
            {
                if (runtime == null) runtime = new StatsRuntime();
                if (statsConfig != null) runtime.Reset(statsConfig);      // seed defaults

                if (statsUI != null)
                {
                    if (statsConfig != null) statsUI.ApplyConfig(statsConfig, alsoSetDefaultsToUI: true); // ranges + default paint
                    statsUI.Init(runtime, syncNow: true);                                                  // subscribe + ensure paint
                    _statsBound = true;
                }
            }

            DeactivateAllVisuals();
        }



        void Start()
        {
            if (autoStart) Restart();
        }

        public void Restart()
        {
            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (scenario == null || scenario.steps == null || scenario.steps.Count == 0)
                yield break;

            int idx = 0;

            while (idx >= 0 && idx < scenario.steps.Count)
            {
                StepIndex = idx;
                var step = scenario.steps[idx];
                if (step == null) { idx++; continue; }

                // make sure only visuals of the current step can be seen or clicked
                DeactivateAllVisuals();

                string branchGuid = null;

                if (step is TimelineStep tl)
                {
                    yield return RunTimeline(tl);
                    branchGuid = tl.nextGuid;
                }
                else if (step is CueCardsStep cc)
                {
                    yield return RunCueCards(cc);
                    branchGuid = cc.nextGuid;
                }
                else if (step is QuestionStep q)
                {
                    _nextGuidFromChoice = null;
                    yield return RunQuestion(q);
                    branchGuid = _nextGuidFromChoice; // null or ""
                }

                // compute next index. empty guid means "next in list"
                if (string.IsNullOrEmpty(branchGuid))
                {
                    idx = idx + 1;
                }
                else
                {
                    int jump = FindIndexByGuid(branchGuid);
                    idx = jump >= 0 ? jump : idx + 1;
                }

                yield return null;
            }

            DeactivateAllVisuals();
            StepIndex = -1;
            _run = null;
        }

        int FindIndexByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid) || scenario?.steps == null) return -1;
            for (int i = 0; i < scenario.steps.Count; i++)
                if (scenario.steps[i] != null && scenario.steps[i].guid == guid)
                    return i;
            return -1;
        }

        // ---------------- TIMELINE ----------------
        IEnumerator RunTimeline(TimelineStep tl)
        {
            var d = tl.director;
            if (!d) yield break;

            // Proper rewind
            if (tl.rewindOnEnter)
            {
                d.time = 0;
                d.Evaluate();           // ensure graph jumps to t=0 immediately
            }

            d.Play();
            yield return null;          // let it start this frame

            if (!tl.waitForEnd)
                yield break;

            bool done = false;
            void OnStopped(PlayableDirector _) => done = true;
            d.stopped += OnStopped;

            // Fallback polling: treat as finished when we’re at/after duration
            // and it's not looping (or state stopped playing).
            const double Eps = 1e-3;
            while (!done)
            {
                // if timeline is not looping and we've reached (or passed) the end
                bool atEnd = d.duration > 0 &&
                             d.extrapolationMode != DirectorWrapMode.Loop &&
                             d.time >= d.duration - Eps;

                if (atEnd || d.state != PlayState.Playing)
                    done = true;

                yield return null;
            }

            d.stopped -= OnStopped;
        }


        // ---------------- CUE CARDS ----------------
        IEnumerator RunCueCards(CueCardsStep cc)
        {
            var cards = cc.cards;
            if (cards == null || cards.Length == 0) yield break;

            // hide all first
            for (int i = 0; i < cards.Length; i++) SafeSet(cards[i], false);

            // use director only as an optional clock
            var d = cc.director;
            if (d && d.state != PlayState.Playing) d.Play();

            // wait release so a click from a previous step does not get consumed here
            yield return WaitForPointerRelease();

            int cur = cc.autoShowFirst ? 0 : -1;
            if (cur == 0) SafeSet(cards[cur], true);

            while (true)
            {
                // if not auto showing the first card wait for first click to reveal it
                if (cur < 0)
                {
                    yield return WaitForCleanClick();
                    cur = 0;
                    SafeSet(cards[cur], true);
                }

                // card timeout (0 = no timeout)
                float timeout = 0f;
                if (cc.cueTimes != null && cc.cueTimes.Length > 0)
                    timeout = (cc.cueTimes.Length == 1) ? cc.cueTimes[0]
                              : (cur < cc.cueTimes.Length ? cc.cueTimes[cur] : 0f);

                // wait for click or timeout
                float t = 0f;
                while (true)
                {
                    if (JustClicked()) break;

                    if (timeout > 0f)
                    {
                        if (d && d.state != PlayState.Playing) break;
                        t += Time.deltaTime;
                        if (t >= timeout) break;
                    }

                    yield return null;
                }

                // consume click so it does not skip next card
                yield return WaitForPointerRelease();

                // advance
                SafeSet(cards[cur], false);

                if (cur >= cards.Length - 1) break;

                cur++;
                SafeSet(cards[cur], true);
            }

            // all off at end
            for (int i = 0; i < cards.Length; i++) SafeSet(cards[i], false);
        }

        // ---------------- QUESTION ----------------
        IEnumerator RunQuestion(QuestionStep q)
        {
            // show and enable only now
            if (q.panelRoot) q.panelRoot.gameObject.SetActive(true);
            if (q.panelAnimator && !string.IsNullOrEmpty(q.showTrigger))
                q.panelAnimator.SetTrigger(q.showTrigger);

            _wired.Clear();
            _nextGuidFromChoice = null;

            if (q.choices != null)
            {
                for (int i = 0; i < q.choices.Count; i++)
                {
                    int idx = i;
                    var choice = q.choices[idx];
                    if (choice == null || choice.button == null) continue;

                    UnityAction fn = () =>
                    {
                        // Only do stats work if feature is present (UI or Config).
                        if (statsUI != null || statsConfig != null)
                        {
                            if (runtime == null)
                                runtime = new StatsRuntime();   // ← no args

                            // if (statsConfig != null) runtime.Apply(statsConfig); // optional

                            if (statsUI != null && !_statsBound)
                            {
                                statsUI.Init(runtime, syncNow: true);
                                _statsBound = true;
                            }

                            if (choice.effects != null)
                            {
                                foreach (var eff in choice.effects)
                                {
                                    if (eff == null) continue;
                                    var cur = runtime[eff.key];
                                    var nxt = eff.Apply(cur);
                                    runtime[eff.key] = nxt;
                                }
                            }
                        }


                        _nextGuidFromChoice = choice.nextGuid;

                        // hide
                        if (q.panelAnimator && !string.IsNullOrEmpty(q.hideTrigger))
                            q.panelAnimator.SetTrigger(q.hideTrigger);
                        else if (q.panelRoot)
                            q.panelRoot.gameObject.SetActive(false);
                    };


                    choice.button.onClick.AddListener(fn);
                    _wired.Add((choice.button, fn));
                }
            }

            // wait for a click only. no auto advance. user must choose
            while (string.IsNullOrEmpty(_nextGuidFromChoice))
                yield return null;

            // remove listeners to avoid double fires later
            if (_wired.Count > 0)
            {
                foreach (var (btn, fn) in _wired) if (btn) btn.onClick.RemoveListener(fn);
                _wired.Clear();
            }

            // make sure panel is not left active
            if (q.panelRoot) q.panelRoot.gameObject.SetActive(false);

            // debounce so the click that chose the option does not also click the next step
            yield return WaitForPointerRelease();
        }

        // ---------------- helpers ----------------
        static bool AnyPointerDown()
        {
            if (Input.GetMouseButton(0)) return true;
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var ph = Input.GetTouch(i).phase;
                    if (ph == TouchPhase.Began || ph == TouchPhase.Moved || ph == TouchPhase.Stationary) return true;
                }
            }
            return false;
        }

        static bool JustClicked()
        {
            if (Input.GetMouseButtonDown(0)) return true;
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
            return false;
        }

        static IEnumerator WaitForPointerRelease()
        {
            // wait until there is no mouse button held and no active touch
            while (AnyPointerDown()) yield return null;
        }

        static IEnumerator WaitForCleanClick()
        {
            // wait for the next press then release (debounced)
            while (!JustClicked()) yield return null;
            while (AnyPointerDown()) yield return null;
        }

        static void SafeSet(GameObject go, bool on)
        {
            if (go && go.activeSelf != on) go.SetActive(on);
        }

        /// Disable all visuals of all steps so nothing is interactable until its turn
        void DeactivateAllVisuals()
        {
            if (scenario?.steps == null) return;

            foreach (var s in scenario.steps)
            {
                if (s is CueCardsStep cc && cc.cards != null)
                {
                    foreach (var card in cc.cards) SafeSet(card, false);
                    if (cc.extraObject) SafeSet(cc.extraObject, false);
                    if (cc.tapHint) SafeSet(cc.tapHint, false);
                }
                else if (s is QuestionStep q)
                {
                    if (q.panelRoot) SafeSet(q.panelRoot.gameObject, false);
                }
            }
        }
    }
}
