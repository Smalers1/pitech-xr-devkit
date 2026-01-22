using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.UI;
using Pitech.XR.Stats;
using Pitech.XR.Interactables;
using Pitech.XR.Quiz;
using UnityEngine.Serialization;
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

        [Header("Quiz (optional)")]
        [FormerlySerializedAs("quiz")]
        public QuizAsset defaultQuiz;

        [FormerlySerializedAs("quizUI")]
        public QuizUIController quizPanel;

        [FormerlySerializedAs("quizResultsUI")]
        public QuizResultsUIController quizResultsPanel;
        public QuizSession quizSession;

        [Header("Interactables (optional)")]
        public SelectablesManager selectables;     // the catalog of clickable colliders
        public SelectionLists selectionLists;      // the quiz/controller using that catalog

        /// Current step index while running. -1 when idle or finished
        public int StepIndex { get; private set; } = -1;

        Coroutine _run;
        sealed class StepRunContext
        {
            public bool cancelRequested;
            public System.Action cancel;

            public void Cancel()
            {
                if (cancelRequested) return;
                cancelRequested = true;
                cancel?.Invoke();
            }
        }

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

            // Optional: auto-wire quiz UI controllers if present in scene.
            if (quizPanel == null)
                quizPanel = UnityEngine.Object.FindObjectOfType<QuizUIController>(true);
            if (quizResultsPanel == null)
                quizResultsPanel = UnityEngine.Object.FindObjectOfType<QuizResultsUIController>(true);

            // Hide (without disabling, if CanvasGroup is present)
            if (quizPanel != null) quizPanel.Hide();
            if (quizResultsPanel != null) quizResultsPanel.Hide();
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
                    yield return RunQuestion(q, guid => branchGuid = guid);
                }
                else if (step is SelectionStep sel)
                {
                    yield return RunSelection(sel, guid => branchGuid = guid);
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
                else if (step is GroupStep g)
                {
                    yield return RunGroup(g);
                    branchGuid = g.nextGuid;
                }
                else if (step is QuizStep qz)
                {
                    yield return RunQuiz(qz, guid => branchGuid = guid);
                }
                else if (step is QuizResultsStep qrs)
                {
                    yield return RunQuizResults(qrs, guid => branchGuid = guid);
                }
                else if (step is MiniQuizStep mq)
                {
                    yield return RunMiniQuiz(mq, guid => branchGuid = guid);
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
        IEnumerator RunQuestion(QuestionStep q, System.Action<string> onChoice, StepRunContext ctx = null)
        {
            // show and enable only now
            if (q.panelRoot) q.panelRoot.gameObject.SetActive(true);
            if (q.panelAnimator && !string.IsNullOrEmpty(q.showTrigger))
                q.panelAnimator.SetTrigger(q.showTrigger);

            var wired = new List<(Button btn, UnityAction fn)>();
            string nextGuid = null;

            void Cleanup()
            {
                if (wired.Count > 0)
                {
                    foreach (var (btn, fn) in wired) if (btn) btn.onClick.RemoveListener(fn);
                    wired.Clear();
                }
                if (q.panelRoot) q.panelRoot.gameObject.SetActive(false);
            }

            if (ctx != null)
                ctx.cancel = Cleanup;

            if (q.choices != null)
            {
                for (int i = 0; i < q.choices.Count; i++)
                {
                    int idx = i;
                    var choice = q.choices[idx];
                    if (choice == null || choice.button == null) continue;

                    UnityAction fn = () =>
                    {
                        // per-choice events (SFX, animations, etc.)
                        choice.onSelected?.Invoke();

                        // apply stat effects (shared helper)
                        ApplyEffects(choice.effects);

                        // IMPORTANT: use FallbackGuid so "" means "linear next"
                        nextGuid = FallbackGuid(choice.nextGuid);

                        // hide
                        if (q.panelAnimator && !string.IsNullOrEmpty(q.hideTrigger))
                            q.panelAnimator.SetTrigger(q.hideTrigger);
                        else if (q.panelRoot)
                            q.panelRoot.gameObject.SetActive(false);
                    };

                    choice.button.onClick.AddListener(fn);
                    wired.Add((choice.button, fn));
                }
            }

            // wait until *something* sets it (normal click or editor skip)
            while (nextGuid == null)
            {
                if (ctx != null && ctx.cancelRequested)
                    break;

                if (_editorSkip && _editorSkipBranchIndex >= 0 && q.choices != null && _editorSkipBranchIndex < q.choices.Count)
                {
                    var choice = q.choices[_editorSkipBranchIndex];
                    if (choice != null)
                    {
                        choice.onSelected?.Invoke();
                        ApplyEffects(choice.effects);
                        nextGuid = FallbackGuid(choice.nextGuid);
                    }
                    break;
                }

                yield return null;
            }

            // remove listeners to avoid double fires later
            Cleanup();

            if (nextGuid != null)
                onChoice?.Invoke(nextGuid);

            // debounce so the click that chose the option does not also click the next step
            if (nextGuid != null)
                yield return WaitForPointerRelease();
        }

        // ---------------- SELECTION ----------------
        IEnumerator RunSelection(SelectionStep s, System.Action<string> onComplete, StepRunContext ctx = null)
        {
            // prefer the step's local reference; fall back to the manager-level field
            var lists = s.lists != null ? s.lists : selectionLists;

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
                onComplete?.Invoke(FallbackGuid(s.wrongNextGuid));
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

            void Cleanup()
            {
                if (submitCb != null && s.submitButton)
                    s.submitButton.onClick.RemoveListener(submitCb);
                if (lists != null && lists.selectables != null)
                    lists.selectables.pickingEnabled = false;
            }

            if (ctx != null)
                ctx.cancel = () =>
                {
                    Cleanup();
                    if (s.hint) s.hint.SetActive(false);
                    if (s.panelRoot) s.panelRoot.gameObject.SetActive(false);
                };

            float t = 0f;
            bool done = false;
            bool isCorrect = false;

            while (!done)
            {
                if (ctx != null && ctx.cancelRequested)
                    break;

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

            Cleanup();

            if (ctx == null || !ctx.cancelRequested)
            {
                // Hide UI & disable picking to avoid spill into next step
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
                onComplete?.Invoke(isCorrect ? FallbackGuid(s.correctNextGuid) : FallbackGuid(s.wrongNextGuid));

                // debounce any final click (especially on submit button)
                yield return WaitForPointerRelease();
            }
        }

        // ---------------- QUIZ ----------------
        IEnumerator RunQuiz(QuizStep qz, System.Action<string> onComplete)
        {
            var asset = qz.quiz != null ? qz.quiz : defaultQuiz;
            if (asset == null)
            {
                Debug.LogWarning("[Scenario] QuizStep: no QuizAsset assigned.");
                yield break;
            }

            QuizAsset.Question question = null;
            if (!string.IsNullOrEmpty(qz.questionId))
                question = asset.FindQuestion(qz.questionId);
            if (question == null && qz.questionIndex >= 0 && qz.questionIndex < asset.questions.Count)
                question = asset.questions[qz.questionIndex];

            if (question == null)
            {
                Debug.LogWarning("[Scenario] QuizStep: question not found (check questionId/index).");
                yield break;
            }

            var session = GetOrCreateQuizSession(asset);

            if (quizPanel == null)
            {
                Debug.LogWarning("[Scenario] QuizStep: Quiz UI missing (QuizUIController).");
                yield break;
            }

            bool done = false;
            bool isCorrect = false;
            // Multi-choice always requires submit. Single-choice can be immediate or submit-button based on step setting.
            var submitMode = (question.type == QuizAsset.QuestionType.MultipleChoice)
                ? QuizUIController.SubmitMode.OnSubmitButton
                : (qz.submitMode == QuizStep.AnswerSubmitMode.OnSubmitButton
                    ? QuizUIController.SubmitMode.OnSubmitButton
                    : QuizUIController.SubmitMode.ImmediateSelection);

            var feedbackMode =
                qz.feedback == QuizStep.FeedbackMode.ForSeconds ? QuizUIController.FeedbackMode.ForSeconds :
                qz.feedback == QuizStep.FeedbackMode.UntilContinue ? QuizUIController.FeedbackMode.UntilContinue :
                QuizUIController.FeedbackMode.None;

            quizPanel.ShowQuestion(question, asset, session, result =>
            {
                isCorrect = result != null && result.isCorrect;
                ApplyQuizStats(session, asset);
                done = true;
            },
            submitMode,
            feedbackMode,
            qz.feedbackSeconds);

            while (!done)
            {
                // Editor skip support: allows jumping via graph without clicking UI.
                if (_editorSkip)
                {
                    // Branch index: -2 correct, -3 wrong, else advance
                    isCorrect = _editorSkipBranchIndex == -2;
                    quizPanel.Hide();
                    done = true;
                }
                yield return null;
            }

            string next = qz.completion == QuizStep.CompleteMode.BranchOnCorrectness
                ? (isCorrect ? FallbackGuid(qz.correctNextGuid) : FallbackGuid(qz.wrongNextGuid))
                : FallbackGuid(qz.nextGuid);
            onComplete?.Invoke(next);

            yield return WaitForPointerRelease();
        }

        void ApplyQuizStats(QuizSession session, QuizAsset asset)
        {
            if ((statsUI == null && statsConfig == null) || session == null) return;

            if (runtime == null)
                runtime = new StatsRuntime();

            if (statsUI != null && !_statsBound)
            {
                statsUI.Init(runtime, syncNow: true);
                _statsBound = true;
            }

            // Update stats frequently, but don't spam "quiz completed" events.
            var summary = session.BuildSummary(invokeEvent: false);
            runtime["Quiz.Score"] = summary.totalScore;
            runtime["Quiz.MaxScore"] = summary.maxScore;
            runtime["Quiz.CorrectCount"] = summary.correctCount;
            runtime["Quiz.WrongCount"] = summary.wrongCount;
            runtime["Quiz.AnsweredCount"] = summary.answeredCount;
            runtime["Quiz.TotalQuestions"] = asset != null && asset.questions != null ? asset.questions.Count : 0;
        }

        IEnumerator RunQuizResults(QuizResultsStep rs, System.Action<string> onComplete)
        {
            var asset = rs.quiz != null ? rs.quiz : defaultQuiz;
            if (asset == null)
            {
                Debug.LogWarning("[Scenario] QuizResultsStep: no QuizAsset assigned.");
                onComplete?.Invoke(FallbackGuid(rs.nextGuid));
                yield break;
            }

            var session = GetOrCreateQuizSession(asset);
            var summary = session != null ? session.BuildSummary(invokeEvent: true) : null;

            if (quizResultsPanel == null)
            {
                Debug.LogWarning("[Scenario] QuizResultsStep: Quiz Results UI missing (QuizResultsUIController).");
            }
            else
            {
                bool done = false;
                // Configure continue button visibility based on "When Complete".
                bool wantsContinue = rs.whenComplete == QuizResultsStep.WhenComplete.AfterContinueButtonPressed;
                if (quizResultsPanel.continueButton != null)
                    quizResultsPanel.continueButton.gameObject.SetActive(wantsContinue);

                quizResultsPanel.Show(asset, summary, () => done = true);

                if (wantsContinue)
                {
                    while (!done)
                    {
                        if (_editorSkip)
                        {
                            done = true;
                            quizResultsPanel.Hide();
                        }
                        yield return null;
                    }
                }
                else
                {
                    float seconds = Mathf.Max(0f, rs.completeAfterSeconds);
                    float t = 0f;
                    while (t < seconds)
                    {
                        if (_editorSkip)
                        {
                            done = true;
                            quizResultsPanel.Hide();
                            break;
                        }
                        t += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                quizResultsPanel.Hide();
                yield return WaitForPointerRelease();
            }

            bool passed = summary != null && (asset.passThresholdPercent <= 0f || summary.passed);
            string next = rs.completion == QuizResultsStep.CompleteMode.BranchOnPassed
                ? (passed ? FallbackGuid(rs.passedNextGuid) : FallbackGuid(rs.failedNextGuid))
                : FallbackGuid(rs.nextGuid);

            onComplete?.Invoke(next);
        }

        // ---------------- MINI QUIZ ----------------
        IEnumerator RunMiniQuiz(MiniQuizStep s, System.Action<string> onComplete, StepRunContext ctx = null)
        {
            if (s == null)
                yield break;

            // show and enable only now
            if (s.panelRoot) s.panelRoot.gameObject.SetActive(true);
            if (s.panelAnimator && !string.IsNullOrEmpty(s.showTrigger))
                s.panelAnimator.SetTrigger(s.showTrigger);

            var wired = new List<(Button btn, UnityAction fn)>();
            UnityAction submitCb = null;

            int correct = 0;
            int qCount = s.questions != null ? s.questions.Count : 0;
            var answered = new bool[Mathf.Max(0, qCount)];

            bool done = false;
            string nextGuid = null;

            void Cleanup()
            {
                if (wired.Count > 0)
                {
                    foreach (var (btn, fn) in wired) if (btn) btn.onClick.RemoveListener(fn);
                    wired.Clear();
                }
                if (submitCb != null && s.submitButton)
                {
                    s.submitButton.onClick.RemoveListener(submitCb);
                    submitCb = null;
                }
                if (s.panelRoot) s.panelRoot.gameObject.SetActive(false);
            }

            if (ctx != null)
                ctx.cancel = Cleanup;

            bool AllAnswered()
            {
                for (int i = 0; i < answered.Length; i++)
                    if (!answered[i]) return false;
                return true;
            }

            // wire all answer buttons
            if (s.questions != null)
            {
                for (int qi = 0; qi < s.questions.Count; qi++)
                {
                    int qIndex = qi;
                    var q = s.questions[qIndex];
                    if (q == null || q.choices == null) continue;

                    for (int ci = 0; ci < q.choices.Count; ci++)
                    {
                        var ch = q.choices[ci];
                        if (ch == null || ch.button == null) continue;

                        UnityAction fn = () =>
                        {
                            // first answer wins (unless user opted out of locking)
                            if (answered[qIndex] && s.lockQuestionAfterAnswer) return;
                            if (!answered[qIndex])
                            {
                                answered[qIndex] = true;
                                if (ch.isCorrect) correct++;
                            }

                            ch.onSelected?.Invoke();
                            ApplyEffects(ch.effects);

                            if (s.lockQuestionAfterAnswer)
                            {
                                // disable all buttons for that question after answering
                                if (q.choices != null)
                                    foreach (var other in q.choices)
                                        if (other != null && other.button != null)
                                            other.button.interactable = false;
                            }

                            if (s.completion == MiniQuizStep.CompleteMode.AutoWhenAllAnswered && AllAnswered())
                                done = true;
                        };

                        ch.button.onClick.AddListener(fn);
                        wired.Add((ch.button, fn));
                    }
                }
            }

            // submit mode
            if (s.completion == MiniQuizStep.CompleteMode.OnSubmitButton && s.submitButton)
            {
                submitCb = () => done = true;
                s.submitButton.onClick.AddListener(submitCb);
            }

            // wait until complete
            while (!done)
            {
                if (ctx != null && ctx.cancelRequested)
                    break;

                // Editor skip (playmode testing from ScenarioGraph)
                if (_editorSkip)
                {
                    if (_editorSkipBranchIndex == -1)
                    {
                        done = true;
                        nextGuid = s.defaultNextGuid;
                    }
                    else if (_editorSkipBranchIndex >= 0 && s.outcomes != null && _editorSkipBranchIndex < s.outcomes.Count)
                    {
                        done = true;
                        nextGuid = s.outcomes[_editorSkipBranchIndex]?.nextGuid;
                    }
                    else
                    {
                        done = true;
                        nextGuid = s.defaultNextGuid;
                    }
                    break;
                }
                yield return null;
            }

            // route by score
            if (nextGuid == null && s.outcomes != null && s.outcomes.Count > 0)
            {
                // Pick the MOST SPECIFIC matching outcome (smallest range),
                // so authoring mistakes like having an "Any (0..-1)" outcome before an "Exact 0..0" outcome
                // still resolve to the intended exact match.
                MiniQuizOutcome best = null;
                int bestSpan = int.MaxValue;

                for (int i = 0; i < s.outcomes.Count; i++)
                {
                    var o = s.outcomes[i];
                    if (o == null) continue;

                    int min = Mathf.Max(0, o.minCorrect);
                    int max = o.maxCorrect;
                    if (max >= 0 && max < min) continue; // invalid range

                    bool minOk = correct >= min;
                    bool maxOk = max < 0 || correct <= max;
                    if (!minOk || !maxOk) continue;

                    int span = max < 0 ? int.MaxValue : Mathf.Max(0, max - min);
                    if (best == null || span < bestSpan)
                    {
                        best = o;
                        bestSpan = span;
                        if (bestSpan == 0) break; // exact match can't be beaten
                    }
                }

                if (best != null)
                    nextGuid = best.nextGuid;
            }

            if (nextGuid == null)
                nextGuid = s.defaultNextGuid;

            if (!string.IsNullOrEmpty(nextGuid))
                Debug.Log($"[Scenario] MiniQuiz: correct={correct}/{(s.questions != null ? s.questions.Count : 0)} -> next={nextGuid}", this);

            Cleanup();

            onComplete?.Invoke(nextGuid);

            // debounce so last click doesn't also hit something behind
            yield return WaitForPointerRelease();
        }

        public QuizSession GetOrCreateQuizSession(QuizAsset asset)
        {
            if (asset == null) return quizSession;

            if (quizSession == null)
                quizSession = new QuizSession(asset);
            else
                quizSession.SetAsset(asset);

            return quizSession;
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
                StopBodyMotion(body);
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
        static void StopBodyMotion(Rigidbody body)
        {
            if (body == null) return;
#if UNITY_6000_0_OR_NEWER
            // Unity 6+
            body.linearVelocity = Vector3.zero;
#else
            // Unity 2022/2023/2024
            body.velocity = Vector3.zero;
#endif
            body.angularVelocity = Vector3.zero;
        }

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

        static void HidePanelRoot(RectTransform rt)
        {
            if (!rt) return;
            var go = rt.gameObject;

            // Prefer CanvasGroup-based hiding (doesn't disable hierarchy)
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
                if (!go.activeSelf) go.SetActive(true);
                return;
            }

            // Common authoring mistake: assigning the whole Canvas as "panelRoot".
            // Never disable a Canvas root; it would blank the entire UI and looks like a bug.
            if (go.GetComponent<Canvas>() != null)
            {
                Debug.LogWarning(
                    "[Scenario] panelRoot points to a Canvas. Please assign a child container GameObject instead (e.g. a Panel under the Canvas).",
                    go);
                return;
            }

            SafeSet(go, false);
        }

        /// Disable all visuals of all steps so nothing is interactable until its turn
        void DeactivateAllVisuals()
        {
            if (scenario?.steps == null) return;

            DeactivateAllVisualsRecursive(scenario.steps);

            if (quizPanel != null) quizPanel.Hide();
            if (quizResultsPanel != null) quizResultsPanel.Hide();
        }

        void DeactivateAllVisualsRecursive(List<Step> list)
        {
            if (list == null) return;

            foreach (var s in list)
            {
                if (s is CueCardsStep cc && cc.cards != null)
                {
                    foreach (var card in cc.cards) SafeSet(card, false);
                    if (cc.extraObject) SafeSet(cc.extraObject, false);
                    if (cc.tapHint) SafeSet(cc.tapHint, false);
                }
                else if (s is QuestionStep q)
                {
                    if (q.panelRoot) HidePanelRoot(q.panelRoot);
                }
                else if (s is SelectionStep sel)
                {
                    if (sel.panelRoot) HidePanelRoot(sel.panelRoot);
                    if (sel.hint) SafeSet(sel.hint, false);
                }
                else if (s is MiniQuizStep mq)
                {
                    if (mq.panelRoot) HidePanelRoot(mq.panelRoot);
                }
                else if (s is GroupStep g && g.steps != null)
                {
                    DeactivateAllVisualsRecursive(g.steps);
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
                current is EventStep ||
                current is GroupStep ||
                current is QuizResultsStep)
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

                _editorSkip = true;
                _editorSkipBranchIndex = branchIndex;
                return;
            }

            // Selection: mark skip and branch type (-2 correct, -3 wrong)
            if (current is SelectionStep sel)
            {
                _editorSkip = true;
                _editorSkipBranchIndex = branchIndex;
                return;
            }

            // Quiz: mark skip and branch type (-2 correct, -3 wrong, else advance)
            if (current is QuizStep)
            {
                _editorSkip = true;
                _editorSkipBranchIndex = branchIndex;
                return;
            }

            // Mini Quiz: -1 = default, >= 0 = outcomes index
            if (current is MiniQuizStep)
            {
                _editorSkip = true;
                _editorSkipBranchIndex = branchIndex;
                return;
            }
        }

        // ---------------- GROUP ----------------
        sealed class GroupCancelToken
        {
            public bool Cancelled { get; private set; }
            public void Cancel() => Cancelled = true;
        }

        sealed class GroupChildHandle
        {
            public Step step;
            public string guid;
            public bool completed;
            public Coroutine coroutine;
            public Action cancel;
        }

        int _groupPickingRefs;

        void SetGroupPicking(SelectablesManager mgr, bool enable)
        {
            if (mgr == null) return;
            if (enable)
            {
                _groupPickingRefs++;
                mgr.pickingEnabled = true;
            }
            else
            {
                _groupPickingRefs = Mathf.Max(0, _groupPickingRefs - 1);
                if (_groupPickingRefs == 0)
                    mgr.pickingEnabled = false;
            }
        }

        IEnumerator RunGroup(GroupStep g)
            => RunGroupInternal(g, null);

        IEnumerator RunGroupInternal(GroupStep g, GroupCancelToken token)
        {
            if (g == null || g.steps == null || g.steps.Count == 0)
                yield break;

            g.EnsureChildRequirements();

            var localToken = token ?? new GroupCancelToken();
            var handles = new List<GroupChildHandle>();

            foreach (var st in g.steps)
            {
                if (st == null) continue;
                if (string.IsNullOrEmpty(st.guid)) st.guid = System.Guid.NewGuid().ToString();
                handles.Add(StartGroupChild(st, localToken));
            }

            bool ShouldComplete()
            {
                if (_editorSkip || localToken.Cancelled) return true;

                int total = handles.Count;
                if (total == 0) return true;

                int completed = 0;
                int requiredTotal = 0;
                int requiredCompleted = 0;
                bool specificDone = false;

                for (int i = 0; i < handles.Count; i++)
                {
                    var h = handles[i];
                    if (h == null) continue;

                    if (h.completed) completed++;

                    bool required = g.IsChildRequired(h.guid);
                    if (required) requiredTotal++;
                    if (required && h.completed) requiredCompleted++;

                    if (!string.IsNullOrEmpty(g.specificStepGuid) && h.guid == g.specificStepGuid && h.completed)
                        specificDone = true;
                }

                switch (g.completeWhen)
                {
                    case GroupStep.CompleteWhen.AnyChildCompletes:
                        return completed > 0;
                    case GroupStep.CompleteWhen.SpecificChildCompletes:
                        return specificDone;
                    case GroupStep.CompleteWhen.RequiredChildrenComplete:
                        return requiredTotal == 0 || requiredCompleted >= requiredTotal;
                    case GroupStep.CompleteWhen.NOfMChildrenComplete:
                        return completed >= Mathf.Clamp(g.requiredCount, 1, total);
                    case GroupStep.CompleteWhen.AllChildrenComplete:
                    default:
                        return completed >= total;
                }
            }

            while (!ShouldComplete())
                yield return null;

            // Stop unfinished children to prevent lingering UI/listeners.
            if (g.stopOthersOnComplete || localToken.Cancelled)
            {
                localToken.Cancel();
                for (int i = 0; i < handles.Count; i++)
                {
                    var h = handles[i];
                    if (h == null || h.completed) continue;
                    h.cancel?.Invoke();
                }
            }

            // Reset skip flag after group ends (like any other step).
            _editorSkip = false;
            _editorSkipBranchIndex = 0;
        }

        GroupChildHandle StartGroupChild(Step st, GroupCancelToken token)
        {
            var h = new GroupChildHandle { step = st, guid = st.guid };

            IEnumerator routine = null;
            Action cleanup = null;

            if (st is TimelineStep tl)
            {
                routine = RunTimelineGroup(tl, token);
                cleanup = () => { if (tl.director) tl.director.Stop(); };
            }
            else if (st is CueCardsStep cc)
            {
                routine = RunCueCardsGroup(cc, token);
                cleanup = () =>
                {
                    if (cc.cards != null) foreach (var card in cc.cards) SafeSet(card, false);
                    if (cc.extraObject) SafeSet(cc.extraObject, false);
                    if (cc.tapHint) SafeSet(cc.tapHint, false);
                };
            }
            else if (st is QuestionStep q)
            {
                routine = RunQuestionGroup(q, token);
                cleanup = () => { if (q.panelRoot) HidePanelRoot(q.panelRoot); };
            }
            else if (st is SelectionStep sel)
            {
                routine = RunSelectionGroup(sel, token);
                cleanup = () =>
                {
                    if (sel.panelRoot) HidePanelRoot(sel.panelRoot);
                    if (sel.hint) SafeSet(sel.hint, false);
                    var lists = sel.lists != null ? sel.lists : selectionLists;
                    if (lists != null && lists.selectables != null) SetGroupPicking(lists.selectables, false);
                };
            }
            else if (st is MiniQuizStep mq)
            {
                routine = RunMiniQuizGroup(mq, token);
                cleanup = () => { if (mq.panelRoot) HidePanelRoot(mq.panelRoot); };
            }
            else if (st is InsertStep ins)
            {
                routine = RunInsertGroup(ins, token);
            }
            else if (st is EventStep ev)
            {
                routine = RunEventGroup(ev, token);
            }
            else if (st is GroupStep g)
            {
                routine = RunGroupInternal(g, token);
            }

            h.cancel = () => cleanup?.Invoke();

            if (routine != null)
            {
                h.coroutine = StartCoroutine(WrapGroupChild(routine, () => h.completed = true));
            }
            else
            {
                h.completed = true;
            }

            return h;
        }

        IEnumerator RunMiniQuizGroup(MiniQuizStep s, GroupCancelToken token)
        {
            if (s == null) yield break;
            if (s.panelRoot) s.panelRoot.gameObject.SetActive(true);
            if (s.panelAnimator && !string.IsNullOrEmpty(s.showTrigger))
                s.panelAnimator.SetTrigger(s.showTrigger);

            var wired = new List<(Button btn, UnityAction fn)>();
            UnityAction submitCb = null;
            int correct = 0;
            int qCount = s.questions != null ? s.questions.Count : 0;
            var answered = new bool[Mathf.Max(0, qCount)];
            bool done = false;

            bool AllAnswered()
            {
                for (int i = 0; i < answered.Length; i++)
                    if (!answered[i]) return false;
                return true;
            }

            if (s.questions != null)
            {
                for (int qi = 0; qi < s.questions.Count; qi++)
                {
                    int qIndex = qi;
                    var q = s.questions[qIndex];
                    if (q == null || q.choices == null) continue;
                    for (int ci = 0; ci < q.choices.Count; ci++)
                    {
                        var ch = q.choices[ci];
                        if (ch == null || ch.button == null) continue;

                        UnityAction fn = () =>
                        {
                            if (answered[qIndex] && s.lockQuestionAfterAnswer) return;
                            if (!answered[qIndex])
                            {
                                answered[qIndex] = true;
                                if (ch.isCorrect) correct++;
                            }

                            ch.onSelected?.Invoke();
                            ApplyEffects(ch.effects);

                            if (s.lockQuestionAfterAnswer)
                            {
                                if (q.choices != null)
                                    foreach (var other in q.choices)
                                        if (other != null && other.button != null)
                                            other.button.interactable = false;
                            }

                            if (s.completion == MiniQuizStep.CompleteMode.AutoWhenAllAnswered && AllAnswered())
                                done = true;
                        };

                        ch.button.onClick.AddListener(fn);
                        wired.Add((ch.button, fn));
                    }
                }
            }

            if (s.completion == MiniQuizStep.CompleteMode.OnSubmitButton && s.submitButton)
            {
                submitCb = () => done = true;
                s.submitButton.onClick.AddListener(submitCb);
            }

            while (!done && !token.Cancelled)
                yield return null;

            foreach (var (btn, fn) in wired)
                if (btn) btn.onClick.RemoveListener(fn);
            if (submitCb != null && s.submitButton) s.submitButton.onClick.RemoveListener(submitCb);

            if (s.panelRoot) s.panelRoot.gameObject.SetActive(false);

            if (!token.Cancelled)
                yield return WaitForPointerRelease();
        }

        IEnumerator WrapGroupChild(IEnumerator routine, Action onDone)
        {
            yield return routine;
            onDone?.Invoke();
        }

        IEnumerator RunTimelineGroup(TimelineStep tl, GroupCancelToken token)
        {
            var d = tl.director;
            if (!d) yield break;

            if (tl.rewindOnEnter)
            {
                d.time = 0;
                d.Evaluate();
            }

            d.Play();
            yield return null;

            if (!tl.waitForEnd) yield break;

            bool done = false;
            void OnStopped(PlayableDirector _) => done = true;
            d.stopped += OnStopped;

            const double Eps = 1e-3;
            while (!done && !token.Cancelled)
            {
                if (d.duration > 0 &&
                    d.extrapolationMode != DirectorWrapMode.Loop &&
                    d.time >= d.duration - Eps)
                    done = true;

                if (d.state != PlayState.Playing)
                    done = true;

                yield return null;
            }

            d.stopped -= OnStopped;
        }

        IEnumerator RunCueCardsGroup(CueCardsStep cc, GroupCancelToken token)
        {
            var cards = cc.cards;
            if (cards == null || cards.Length == 0) yield break;

            for (int i = 0; i < cards.Length; i++) SafeSet(cards[i], false);

            var d = cc.director;
            if (d && d.state != PlayState.Playing) d.Play();

            yield return WaitForPointerRelease();

            int cur = cc.autoShowFirst ? 0 : -1;
            if (cur == 0) SafeSet(cards[cur], true);

            while (!token.Cancelled)
            {
                if (cur < 0)
                {
                    yield return WaitForCleanClick();
                    if (token.Cancelled) break;
                    cur = 0;
                    SafeSet(cards[cur], true);
                }

                float timeout = 0f;
                if (cc.cueTimes != null && cc.cueTimes.Length > 0)
                    timeout = (cc.cueTimes.Length == 1) ? cc.cueTimes[0]
                              : (cur < cc.cueTimes.Length ? cc.cueTimes[cur] : 0f);

                float t = 0f;
                while (!token.Cancelled)
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
                if (token.Cancelled) break;

                yield return WaitForPointerRelease();

                SafeSet(cards[cur], false);
                if (cur >= cards.Length - 1) break;
                cur++;
                SafeSet(cards[cur], true);
            }

            for (int i = 0; i < cards.Length; i++) SafeSet(cards[i], false);
        }

        IEnumerator RunQuestionGroup(QuestionStep q, GroupCancelToken token)
        {
            if (q.panelRoot) q.panelRoot.gameObject.SetActive(true);
            if (q.panelAnimator && !string.IsNullOrEmpty(q.showTrigger))
                q.panelAnimator.SetTrigger(q.showTrigger);

            var wired = new List<(Button btn, UnityAction fn)>();
            bool answered = false;

            if (q.choices != null)
            {
                for (int i = 0; i < q.choices.Count; i++)
                {
                    var choice = q.choices[i];
                    if (choice == null || choice.button == null) continue;

                    UnityAction fn = () =>
                    {
                        if (answered) return;
                        choice.onSelected?.Invoke();
                        ApplyEffects(choice.effects);
                        answered = true;
                        if (q.panelAnimator && !string.IsNullOrEmpty(q.hideTrigger))
                            q.panelAnimator.SetTrigger(q.hideTrigger);
                        else if (q.panelRoot)
                            q.panelRoot.gameObject.SetActive(false);
                    };

                    choice.button.onClick.AddListener(fn);
                    wired.Add((choice.button, fn));
                }
            }

            while (!answered && !token.Cancelled)
                yield return null;

            foreach (var (btn, fn) in wired)
                if (btn) btn.onClick.RemoveListener(fn);

            if (q.panelRoot) q.panelRoot.gameObject.SetActive(false);

            if (!token.Cancelled)
                yield return WaitForPointerRelease();
        }

        IEnumerator RunSelectionGroup(SelectionStep s, GroupCancelToken token)
        {
            var lists = s.lists != null ? s.lists : selectionLists;
            if (lists == null)
            {
                Debug.LogWarning("[Scenario] SelectionStep (Group): no SelectionLists assigned.", this);
                yield break;
            }

            if (s.panelRoot) s.panelRoot.gameObject.SetActive(true);
            if (s.panelAnimator && !string.IsNullOrEmpty(s.showTrigger))
                s.panelAnimator.SetTrigger(s.showTrigger);
            if (s.hint) s.hint.SetActive(true);

            int active = -1;
            if (!string.IsNullOrEmpty(s.listKey))
                active = lists.ShowList(s.listKey, s.resetOnEnter);
            else
                active = lists.ShowList(s.listIndex, s.resetOnEnter);

            if (lists.selectables != null)
                SetGroupPicking(lists.selectables, true);

            bool submitted = false;
            UnityAction submitCb = null;
            if (s.completion == SelectionStep.CompleteMode.OnSubmitButton && s.submitButton)
            {
                submitCb = () => submitted = true;
                s.submitButton.onClick.AddListener(submitCb);
            }

            bool done = false;
            bool isCorrect = false;
            float t = 0f;

            while (!done && !token.Cancelled)
            {
                if (active < 0)
                {
                    isCorrect = false;
                    break;
                }

                if (s.timeoutSeconds > 0f)
                {
                    t += Time.deltaTime;
                    if (t >= s.timeoutSeconds)
                    {
                        isCorrect = false;
                        break;
                    }
                }

                var e = lists.EvaluateActive();
                bool countOK = s.requireExactCount
                    ? (e.selectedTotal == s.requiredSelections)
                    : (e.selectedTotal >= s.requiredSelections);

                if (s.completion == SelectionStep.CompleteMode.AutoWhenRequirementMet)
                {
                    if (countOK)
                    {
                        bool wrongOK = e.selectedWrong <= s.allowedWrong;
                        isCorrect = wrongOK;
                        done = true;
                    }
                }
                else
                {
                    if (submitted)
                    {
                        bool wrongOK = e.selectedWrong <= s.allowedWrong;
                        isCorrect = countOK && wrongOK;
                        done = true;
                    }
                }

                yield return null;
            }

            if (submitCb != null && s.submitButton)
                s.submitButton.onClick.RemoveListener(submitCb);

            if (lists.selectables != null)
                SetGroupPicking(lists.selectables, false);

            if (s.hint) s.hint.SetActive(false);
            if (s.panelAnimator && !string.IsNullOrEmpty(s.hideTrigger))
                s.panelAnimator.SetTrigger(s.hideTrigger);
            else if (s.panelRoot)
                s.panelRoot.gameObject.SetActive(false);

            if (token.Cancelled)
                yield break;

            try
            {
                if (isCorrect) s.onCorrect?.Invoke();
                else s.onWrong?.Invoke();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }

            if (isCorrect) ApplyEffects(s.onCorrectEffects);
            else ApplyEffects(s.onWrongEffects);

            yield return WaitForPointerRelease();
        }

        IEnumerator RunInsertGroup(InsertStep ins, GroupCancelToken token)
        {
            if (ins == null || ins.item == null || ins.targetTrigger == null)
            {
                Debug.LogWarning("[Scenario] InsertStep requires Item and TargetTrigger.", this);
                yield break;
            }

            SafeSet(ins.item.gameObject, true);
            SafeSet(ins.targetTrigger.gameObject, true);

            yield return WaitForPointerRelease();

            var itemColliders = ins.item.GetComponentsInChildren<Collider>();
            if (itemColliders == null || itemColliders.Length == 0)
            {
                Debug.LogWarning("[Scenario] InsertStep: Item has no Colliders. Completing immediately.", this);
            }
            else
            {
                bool hit = false;
                while (!hit && !token.Cancelled)
                {
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

            if (token.Cancelled)
                yield break;

            var body = ins.item.GetComponentInChildren<Rigidbody>();
            if (body != null)
            {
                StopBodyMotion(body);
            }

            if (ins.smoothAttach)
            {
                Transform targetPose =
                    ins.attachTransform != null
                        ? ins.attachTransform
                        : ins.targetTrigger.transform;

                if (targetPose != null)
                {
                    if (body != null)
                        body.isKinematic = true;

                    if (ins.parentToAttach)
                        ins.item.SetParent(targetPose, true);

                    while (!token.Cancelled)
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

            if (!token.Cancelled)
                yield return WaitForPointerRelease();
        }

        IEnumerator RunEventGroup(EventStep ev, GroupCancelToken token)
        {
            if (ev == null)
                yield break;

            try
            {
                ev.onEnter?.Invoke();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }

            float wait = ev.waitSeconds;
            if (wait > 0f)
            {
                float t = 0f;
                while (t < wait && !token.Cancelled)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
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
