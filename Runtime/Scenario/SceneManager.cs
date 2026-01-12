using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.UI;
using Pitech.XR.Stats;
using Pitech.XR.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


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

        [Header("Interactables (optional)")]
        public SelectablesManager selectables;     // the catalog of clickable colliders
        public SelectionLists selectionLists;      // the quiz/controller using that catalog

        /// Current step index while running. -1 when idle or finished
        public int StepIndex { get; private set; } = -1;

        Coroutine _run;
        readonly List<(Button btn, UnityAction fn)> _wired = new();
        string _nextGuidFromChoice;
        string _nextGuidFromSelection;

        bool _editorSkip;
        int _editorSkipBranchIndex;

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

            if (selectionLists != null)
            {
                if (selectionLists.selectables == null && selectables != null)
                    selectionLists.selectables = selectables;
            }
            if (selectables != null)
                selectables.pickingEnabled = false;

            DeactivateAllVisuals();
        }

        // ------ Convenience bridges (so Timeline/UI can talk only to SceneManager) ------
        public void ActivateSelectionList(int index) => selectionLists?.ActivateList(index);
        public void ActivateSelectionListByName(string listName) => selectionLists?.ActivateListByName(listName);
        public void CompleteSelection() => selectionLists?.CompleteActive();
        public void RetrySelection() => selectionLists?.RetryActive();

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
                else if (step is SelectionStep sel)
                {
                    _nextGuidFromSelection = null;
                    yield return RunSelection(sel);
                    branchGuid = _nextGuidFromSelection; // null or ""
                }
                else if (step is InsertStep ins)
                {
                    yield return RunInsert(ins);
                    branchGuid = ins.nextGuid;
                }
                else if (step is EventStep ev)
                {
                    yield return RunEvent(ev);
                    branchGuid = ev.nextGuid;
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

                // reset editor skip flags after each step
                _editorSkip = false;
                _editorSkipBranchIndex = 0;

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
                if (_editorSkip)
                {
                    done = true;
                    break;
                }

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
                if (_editorSkip)
                    break;

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
                        // apply stat effects (shared helper)
                        ApplyEffects(choice.effects);

                        // IMPORTANT: use FallbackGuid so "" means "linear next"
                        _nextGuidFromChoice = FallbackGuid(choice.nextGuid);

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

            // wait until *something* sets it (normal click or editor skip)
            while (_nextGuidFromChoice == null)
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

        // ---------------- SELECTION ----------------
        IEnumerator RunSelection(SelectionStep s)
        {
            // prefer the step's local reference; fall back to the manager-level field
            var lists = s.lists != null ? s.lists : selectionLists;
            _nextGuidFromSelection = null;

            // show and enable only now
            if (s.panelRoot) s.panelRoot.gameObject.SetActive(true);
            if (s.panelAnimator && !string.IsNullOrEmpty(s.showTrigger))
                s.panelAnimator.SetTrigger(s.showTrigger);
            if (s.hint) s.hint.SetActive(true);

            // Activate requested list
            int active = -1;
            if (lists != null)
            {
                if (!string.IsNullOrEmpty(s.listKey))
                    active = lists.ShowList(s.listKey, s.resetOnEnter);
                else
                    active = lists.ShowList(s.listIndex, s.resetOnEnter);
            }

            if (active < 0)
            {
                Debug.LogWarning("[Scenario] SelectionStep: could not activate requested list. Will route WRONG.");
                yield return HideSelectionUI(s);
                _nextGuidFromSelection = FallbackGuid(s.wrongNextGuid);
                yield break;
            }

            // Optional submit wiring
            bool submitted = false;
            UnityAction submitCb = null;
            if (s.completion == SelectionStep.CompleteMode.OnSubmitButton && s.submitButton)
            {
                submitCb = () => submitted = true;
                s.submitButton.onClick.AddListener(submitCb);
            }

            float t = 0f;
            bool done = false;
            bool isCorrect = false;

            while (!done)
            {
                // timeout => WRONG
                if (s.timeoutSeconds > 0f)
                {
                    t += Time.deltaTime;
                    if (t >= s.timeoutSeconds)
                    {
                        isCorrect = false;
                        break;
                    }
                }
                // Editor graph skip override
                if (_editorSkip)
                {
                    if (_editorSkipBranchIndex == -2) // Correct
                    {
                        isCorrect = true;
                        done = true;
                    }
                    else if (_editorSkipBranchIndex == -3) // Wrong
                    {
                        isCorrect = false;
                        done = true;
                    }
                }

                // Always evaluate (cheap + robust even if no OnSelectionChanged is fired)
                var e = lists.EvaluateActive();

                bool countOK = s.requireExactCount
                    ? (e.selectedTotal == s.requiredSelections)
                    : (e.selectedTotal >= s.requiredSelections);

                if (s.completion == SelectionStep.CompleteMode.AutoWhenRequirementMet)
                {
                    if (countOK)
                    {
                        // correctness: within wrong tolerance
                        bool wrongOK = e.selectedWrong <= s.allowedWrong;
                        isCorrect = wrongOK;                 // <- removed the "e.selectedCorrect > 0" gate
                        done = true;
                    }
                }
                else // OnSubmitButton
                {
                    if (submitted)
                    {
                        bool wrongOK = e.selectedWrong <= s.allowedWrong;
                        isCorrect = countOK && wrongOK;     // <- also no "must have ≥1 correct" gate
                        done = true;
                    }
                }

                yield return null;
            }


            // Cleanup listeners
            if (submitCb != null && s.submitButton)
                s.submitButton.onClick.RemoveListener(submitCb);


            // Hide UI & disable picking to avoid spill into next step
            if (lists.selectables != null) lists.selectables.pickingEnabled = false;
            yield return HideSelectionUI(s);

            // Events
            try
            {
                if (isCorrect) s.onCorrect?.Invoke();
                else s.onWrong?.Invoke();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }

            // (Optional) Stats lists – still supported if you didn’t remove them
            if (isCorrect) ApplyEffects(s.onCorrectEffects);
            else ApplyEffects(s.onWrongEffects);

            // Route
            _nextGuidFromSelection = isCorrect ? FallbackGuid(s.correctNextGuid) : FallbackGuid(s.wrongNextGuid);

            // debounce any final click (especially on submit button)
            yield return WaitForPointerRelease();
        }

        IEnumerator HideSelectionUI(SelectionStep s)
        {
            if (s.hint) s.hint.SetActive(false);
            if (s.panelAnimator && !string.IsNullOrEmpty(s.hideTrigger))
                s.panelAnimator.SetTrigger(s.hideTrigger);
            else if (s.panelRoot)
                s.panelRoot.gameObject.SetActive(false);
            yield return null; // settle one frame
        }

        static string FallbackGuid(string prefer)
        {
            // empty => linear next
            return string.IsNullOrEmpty(prefer) ? "" : prefer;
        }

        void ApplyEffects(List<StatEffect> effects)
        {
            // Only do stats work if feature is present (UI or Config).
            if ((statsUI == null && statsConfig == null) || effects == null || effects.Count == 0)
                return;

            if (runtime == null)
                runtime = new StatsRuntime();

            if (statsUI != null && !_statsBound)
            {
                statsUI.Init(runtime, syncNow: true);
                _statsBound = true;
            }

            foreach (var eff in effects)
            {
                if (eff == null) continue;
                var cur = runtime[eff.key];
                var nxt = eff.Apply(cur);
                runtime[eff.key] = nxt;
            }
        }

        // ---------------- INSERT ----------------
        IEnumerator RunInsert(InsertStep ins)
        {
            if (ins == null || ins.item == null || ins.targetTrigger == null)
            {
                Debug.LogWarning("[Scenario] InsertStep requires Item and TargetTrigger.", this);
                yield break;
            }

            // Ensure item & trigger visible
            SafeSet(ins.item.gameObject, true);
            SafeSet(ins.targetTrigger.gameObject, true);

            // Avoid consuming previous click
            yield return WaitForPointerRelease();

            // Find all item colliders
            var itemColliders = ins.item.GetComponentsInChildren<Collider>();
            if (itemColliders == null || itemColliders.Length == 0)
            {
                Debug.LogWarning("[Scenario] InsertStep: Item has no Colliders. Completing immediately.", this);
            }
            else
            {
                bool hit = false;

                while (!hit)
                {
                    if (_editorSkip)
                        break;   // bail out early on editor skip

                    if (!ins.targetTrigger)
                        yield break;

                    foreach (var col in itemColliders)
                    {
                        if (!col) continue;
                        if (AreCollidersOverlapping(col, ins.targetTrigger))
                        {
                            hit = true;
                            break;
                        }
                    }

                    if (!hit)
                        yield return null;
                }
            }

            // We consider it "inserted" here
            var body = ins.item.GetComponentInChildren<Rigidbody>();
            if (body != null)
            {
                // Always stop any crazy motion
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                // BUT: do NOT touch isKinematic here
                // that depends on smoothAttach
            }

            // Only if smoothAttach is enabled we do the snapping / parenting style behaviour
            if (ins.smoothAttach)
            {
                Transform targetPose =
                    ins.attachTransform != null
                        ? ins.attachTransform
                        : ins.targetTrigger.transform;

                if (targetPose != null)
                {
                    // For smooth attach we usually want to freeze physics
                    if (body != null)
                        body.isKinematic = true;

                    if (ins.parentToAttach)
                        ins.item.SetParent(targetPose, true);

                    while (true)
                    {
                        ins.item.position = Vector3.MoveTowards(
                            ins.item.position,
                            targetPose.position,
                            ins.moveSpeed * Time.deltaTime
                        );

                        ins.item.rotation = Quaternion.Slerp(
                            ins.item.rotation,
                            targetPose.rotation,
                            ins.rotateSpeed * Time.deltaTime
                        );

                        float posDist = Vector3.Distance(ins.item.position, targetPose.position);
                        float ang = Quaternion.Angle(ins.item.rotation, targetPose.rotation);

                        if (posDist < 0.01f && ang < 1f)
                            break;

                        yield return null;
                    }
                }
            }

            // If smoothAttach == false we did NOT change isKinematic
            // The object just stays where the user left it after entering the trigger

            // Debounce any grab / click
            yield return WaitForPointerRelease();
        }

        // ---------------- EVENT ----------------
        IEnumerator RunEvent(EventStep ev)
        {
            if (ev == null)
                yield break;

            // Fire the event safely
            try
            {
                ev.onEnter?.Invoke();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }

            // Optional wait
            float wait = ev.waitSeconds;
            if (wait > 0f)
            {
                float t = 0f;
                while (t < wait)
                {
                    if (_editorSkip)
                        break; // skip cancels waiting

                    t += Time.deltaTime;
                    yield return null;
                }
            }

            // Then we simply return, and the main Run() loop advances to next step
        }

        // ---------------- helpers ----------------
        static bool AnyPointerDown()
        {
#if ENABLE_INPUT_SYSTEM
            // Αν δεν υπάρχουν pointer devices (VR-only), θεωρούμε ότι "δεν πατιέται τίποτα"
            bool hasMouse = Mouse.current != null;
            bool hasTouch = Touchscreen.current != null;

            if (hasMouse && Mouse.current.leftButton.isPressed) return true;

            if (hasTouch)
            {
                var ts = Touchscreen.current;
                foreach (var t in ts.touches)
                {
                    if (t.press.isPressed) return true;
                }
            }
            return false;
#else
    if (UnityEngine.Input.GetMouseButton(0)) return true;

    for (int i = 0; i < UnityEngine.Input.touchCount; i++)
    {
        var ph = UnityEngine.Input.GetTouch(i).phase;
        if (ph == UnityEngine.TouchPhase.Began ||
            ph == UnityEngine.TouchPhase.Moved ||
            ph == UnityEngine.TouchPhase.Stationary)
            return true;
    }
    return false;
#endif
        }

        static bool JustClicked()
        {
#if ENABLE_INPUT_SYSTEM
            bool hasMouse = Mouse.current != null;
            bool hasTouch = Touchscreen.current != null;

            if (hasMouse && Mouse.current.leftButton.wasPressedThisFrame) return true;

            if (hasTouch)
            {
                var ts = Touchscreen.current;
                foreach (var t in ts.touches)
                {
                    if (t.press.wasPressedThisFrame) return true;
                }
            }
            return false;
#else
    if (UnityEngine.Input.GetMouseButtonDown(0)) return true;

    for (int i = 0; i < UnityEngine.Input.touchCount; i++)
    {
        if (UnityEngine.Input.GetTouch(i).phase == UnityEngine.TouchPhase.Began)
            return true;
    }
    return false;
#endif
        }

        static System.Collections.IEnumerator WaitForPointerRelease()
        {
#if ENABLE_INPUT_SYSTEM
            // Σε VR (χωρίς mouse/touch), μην περιμένεις τίποτα — επέστρεψε άμεσα.
            if (Mouse.current == null && Touchscreen.current == null)
                yield break;
#endif
            while (AnyPointerDown()) yield return null;
        }

        static System.Collections.IEnumerator WaitForCleanClick()
        {
#if ENABLE_INPUT_SYSTEM
            // Σε VR (χωρίς mouse/touch), δεν μπορεί να υπάρξει "clean click" -> μην μπλοκάρεις.
            if (Mouse.current == null && Touchscreen.current == null)
                yield break;
#endif
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
                else if (s is SelectionStep sel)
                {
                    if (sel.panelRoot) SafeSet(sel.panelRoot.gameObject, false);
                    if (sel.hint) SafeSet(sel.hint, false);
                }
                else if (s is InsertStep ins)
                {
                }
            }
        }
        public void EditorSkipFromGraph(string stepGuid, int branchIndex)
        {
            if (!Application.isPlaying) return;
            if (scenario == null || scenario.steps == null) return;

            if (StepIndex < 0 || StepIndex >= scenario.steps.Count) return;

            var current = scenario.steps[StepIndex];
            if (current == null || current.guid != stepGuid) return;

            // Linear-ish steps: just mark skip, RunX θα το διαβάσει
            if (current is TimelineStep ||
                current is CueCardsStep ||
                current is InsertStep ||
                current is EventStep)
            {
                _editorSkip = true;
                _editorSkipBranchIndex = branchIndex;
                return;
            }

            // Question: choose specific choice by index
            if (current is QuestionStep q)
            {
                if (branchIndex < 0) return;
                if (q.choices == null) return;
                if (branchIndex >= q.choices.Count) return;

                var choice = q.choices[branchIndex];
                if (choice == null) return;

                // apply effects like button click
                ApplyEffects(choice.effects);

                _nextGuidFromChoice = FallbackGuid(choice.nextGuid);

                // hide UI like in the normal path
                if (q.panelAnimator && !string.IsNullOrEmpty(q.hideTrigger))
                    q.panelAnimator.SetTrigger(q.hideTrigger);
                else if (q.panelRoot)
                    q.panelRoot.gameObject.SetActive(false);

                return;
            }

            // Selection: mark skip and branch type (-2 correct, -3 wrong)
            if (current is SelectionStep sel)
            {
                _editorSkip = true;
                _editorSkipBranchIndex = branchIndex;
                return;
            }
        }


        static bool AreCollidersOverlapping(Collider a, Collider b)
        {
            if (!a || !b) return false;

            // Ακριβής έλεγχος overlap, ουσιαστικά σαν OnTriggerEnter αλλά με polling
            return Physics.ComputePenetration(
                a, a.transform.position, a.transform.rotation,
                b, b.transform.position, b.transform.rotation,
                out _, out _
            );
        }



    }
}
