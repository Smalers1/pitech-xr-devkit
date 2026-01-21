using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pitech.XR.Quiz
{
    [AddComponentMenu("Pi tech XR/Quiz/Quiz UI Controller")]
    public sealed class QuizUIController : MonoBehaviour
    {
        public enum SubmitMode
        {
            ImmediateSelection,
            OnSubmitButton
        }

        public enum FeedbackMode
        {
            None,
            ForSeconds,
            UntilContinue
        }

        [Header("UI")]
        [Tooltip("Optional CanvasGroup used to show/hide without disabling the GameObject (recommended).")]
        public CanvasGroup canvasGroup;
        public TMP_Text promptText;
        public Transform answersRoot;
        public Button answerButtonPrefab;
        public Button submitButton;

        [Header("Optional Feedback UI")]
        [Tooltip("Optional label for correctness feedback (e.g. 'Correct!' / 'Wrong').")]
        public TMP_Text feedbackText;

        [Tooltip("Optional explanation text area. If assigned, we will display answer explanations here.")]
        public TMP_Text explanationText;

        [Tooltip("Optional continue button for feedback stage. If assigned and the QuizAsset has showCorrectImmediately=true, we wait for Continue before completing the step.")]
        public Button continueButton;

        readonly List<Button> _buttons = new List<Button>();
        readonly HashSet<int> _selected = new HashSet<int>();
        Action<List<int>> _onAnswered; // legacy
        Action<QuizSession.QuestionResult> _onCompleted;
        QuizAsset.Question _current;
        QuizAsset _asset;
        QuizSession _session;
        bool _awaitingContinue;
        QuizSession.QuestionResult _lastResult;
        SubmitMode _submitMode = SubmitMode.ImmediateSelection;
        FeedbackMode _feedbackMode = FeedbackMode.None;
        float _feedbackSeconds = 0f;
        Coroutine _autoAdvance;

        void Awake()
        {
            // Auto-wire CanvasGroup if present on the same object.
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();

            if (continueButton)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(OnContinueClicked);
            }
        }

        public void ShowQuestion(QuizAsset.Question q, Action<List<int>> onAnswered)
        {
            if (q == null) return;
            SetVisible(true);

            _current = q;
            _onAnswered = onAnswered;
            _onCompleted = null;
            _asset = null;
            _session = null;
            _awaitingContinue = false;
            _lastResult = null;
            _submitMode = SubmitMode.ImmediateSelection;
            _feedbackMode = FeedbackMode.None;
            _feedbackSeconds = 0f;
            _selected.Clear();

            if (promptText) promptText.text = q.prompt;
            if (feedbackText) feedbackText.text = "";
            if (explanationText) explanationText.text = "";

            RebuildButtons(q);
            SetSubmitVisible(q.type == QuizAsset.QuestionType.MultipleChoice);
            SetContinueVisible(false);
            RefreshSubmitInteractable();
        }

        /// <summary>
        /// Preferred API: shows a question and evaluates it through the provided session (so we can show feedback/explanations).
        /// </summary>
        public void ShowQuestion(QuizAsset.Question q, QuizAsset asset, QuizSession session, Action<QuizSession.QuestionResult> onCompleted)
            => ShowQuestion(q, asset, session, onCompleted,
                submitMode: SubmitMode.ImmediateSelection,
                feedbackMode: (asset != null && asset.showCorrectImmediately) ? FeedbackMode.UntilContinue : FeedbackMode.None,
                feedbackSeconds: 0f);

        public void ShowQuestion(
            QuizAsset.Question q,
            QuizAsset asset,
            QuizSession session,
            Action<QuizSession.QuestionResult> onCompleted,
            SubmitMode submitMode,
            FeedbackMode feedbackMode,
            float feedbackSeconds)
        {
            if (q == null) return;
            SetVisible(true);

            _current = q;
            _asset = asset;
            _session = session;
            _onCompleted = onCompleted;
            _onAnswered = null;
            _awaitingContinue = false;
            _lastResult = null;
            _submitMode = submitMode;
            _feedbackMode = feedbackMode;
            _feedbackSeconds = Mathf.Max(0f, feedbackSeconds);
            _selected.Clear();

            if (promptText) promptText.text = q.prompt;
            if (feedbackText) feedbackText.text = "";
            if (explanationText) explanationText.text = "";

            RebuildButtons(q);
            bool showSubmit =
                q.type == QuizAsset.QuestionType.MultipleChoice ||
                _submitMode == SubmitMode.OnSubmitButton;
            SetSubmitVisible(showSubmit);
            SetContinueVisible(false);
            RefreshSubmitInteractable();
        }

        public void Hide()
        {
            if (promptText) promptText.text = "";
            if (feedbackText) feedbackText.text = "";
            if (explanationText) explanationText.text = "";
            ClearButtons();
            SetSubmitVisible(false);
            SetContinueVisible(false);
            _current = null;
            _onAnswered = null;
            _onCompleted = null;
            _asset = null;
            _session = null;
            _selected.Clear();
            _awaitingContinue = false;
            _lastResult = null;
            _submitMode = SubmitMode.ImmediateSelection;
            _feedbackMode = FeedbackMode.None;
            _feedbackSeconds = 0f;
            if (_autoAdvance != null) { StopCoroutine(_autoAdvance); _autoAdvance = null; }
            SetVisible(false);
        }

        void SetVisible(bool on)
        {
            // Prefer CanvasGroup so the hierarchy doesn't constantly toggle active state.
            if (canvasGroup)
            {
                canvasGroup.alpha = on ? 1f : 0f;
                canvasGroup.interactable = on;
                canvasGroup.blocksRaycasts = on;
                if (!gameObject.activeSelf) gameObject.SetActive(true);
                return;
            }

            gameObject.SetActive(on);
        }

        void RebuildButtons(QuizAsset.Question q)
        {
            ClearButtons();
            if (answersRoot == null || answerButtonPrefab == null || q.answers == null) return;

            for (int i = 0; i < q.answers.Count; i++)
            {
                int idx = i;
                var a = q.answers[i];
                if (a == null) continue;

                var btn = Instantiate(answerButtonPrefab, answersRoot);
                var text = btn.GetComponentInChildren<TMP_Text>();
                if (text) text.text = a.text;

                btn.onClick.AddListener(() => OnAnswerClicked(idx));
                _buttons.Add(btn);
            }

            if (submitButton)
            {
                submitButton.onClick.RemoveAllListeners();
                submitButton.onClick.AddListener(SubmitAny);
            }
        }

        void OnAnswerClicked(int idx)
        {
            if (_current == null) return;
            if (_awaitingContinue) return;

            if (_current.type == QuizAsset.QuestionType.SingleChoice)
            {
                _selected.Clear();
                _selected.Add(idx);
                if (_submitMode == SubmitMode.ImmediateSelection)
                {
                    SubmitNowOrFeedback();
                }
                else
                {
                    RefreshButtonStates();
                    RefreshSubmitInteractable();
                }
            }
            else
            {
                if (_selected.Contains(idx)) _selected.Remove(idx);
                else _selected.Add(idx);
                RefreshButtonStates();
                RefreshSubmitInteractable();
            }
        }

        void SubmitAny()
        {
            if (_current == null) return;
            if (_selected.Count <= 0) return;
            SubmitNowOrFeedback();
        }

        void SubmitNowOrFeedback()
        {
            var list = new List<int>(_selected);

            // Legacy mode: just return the selected indices immediately.
            if (_onAnswered != null)
            {
                _onAnswered.Invoke(list);
                Hide();
                return;
            }

            // Session-driven mode: evaluate and optionally show feedback before completing.
            if (_session != null && _current != null)
            {
                var result = _session.AnswerQuestion(_current, list);
                _lastResult = result;

                bool showFeedback =
                    _feedbackMode != FeedbackMode.None &&
                    (feedbackText != null || explanationText != null || continueButton != null);

                if (showFeedback)
                {
                    ShowFeedback(result);
                    _awaitingContinue = true;
                    DisableAnswerButtons();
                    SetSubmitVisible(false);
                    SetContinueVisible(_feedbackMode == FeedbackMode.UntilContinue);

                    if (_feedbackMode == FeedbackMode.ForSeconds)
                    {
                        if (_autoAdvance != null) StopCoroutine(_autoAdvance);
                        _autoAdvance = StartCoroutine(AutoAdvanceAfterSeconds(_feedbackSeconds));
                        return;
                    }

                    if (_feedbackMode == FeedbackMode.UntilContinue)
                        return;
                }

                CompleteAndHide();
                return;
            }

            // Fallback: nothing to do, but keep safe.
            Hide();
        }

        System.Collections.IEnumerator AutoAdvanceAfterSeconds(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            CompleteAndHide();
        }

        void CompleteAndHide()
        {
            var cb = _onCompleted;
            var r = _lastResult;
            _onCompleted = null;
            cb?.Invoke(r);
            Hide();
        }

        void RefreshButtonStates()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                var btn = _buttons[i];
                if (!btn) continue;
                var img = btn.GetComponent<Image>();
                if (!img) continue;
                img.color = _selected.Contains(i) ? new Color(0.20f, 0.55f, 1f, 1f) : Color.white;
            }
        }

        void RefreshSubmitInteractable()
        {
            if (!submitButton) return;
            bool shouldShow =
                _current != null &&
                (_current.type == QuizAsset.QuestionType.MultipleChoice ||
                 _submitMode == SubmitMode.OnSubmitButton);
            if (!shouldShow) { submitButton.interactable = true; return; }
            submitButton.interactable = _selected.Count > 0 && !_awaitingContinue;
        }

        void SetSubmitVisible(bool on)
        {
            if (submitButton) submitButton.gameObject.SetActive(on);
        }

        void SetContinueVisible(bool on)
        {
            if (continueButton) continueButton.gameObject.SetActive(on);
        }

        void DisableAnswerButtons()
        {
            for (int i = 0; i < _buttons.Count; i++)
                if (_buttons[i]) _buttons[i].interactable = false;
        }

        void OnContinueClicked()
        {
            if (!_awaitingContinue) return;
            _awaitingContinue = false;
            // We intentionally complete *on* continue so the Scenario doesn't advance until the player reads feedback.
            CompleteAndHide();
        }

        void ShowFeedback(QuizSession.QuestionResult result)
        {
            if (result == null)
            {
                if (feedbackText) feedbackText.text = "";
                if (explanationText) explanationText.text = "";
                return;
            }

            if (feedbackText)
            {
                feedbackText.text = result.isCorrect
                    ? $"Correct (+{result.score:0.#})"
                    : "Wrong";
            }

            if (explanationText && _current != null && _current.answers != null)
            {
                var lines = new List<string>();

                // Prefer showing explanations for selected answers (if any).
                if (result.selected != null && result.selected.Count > 0)
                {
                    foreach (var idx in result.selected)
                    {
                        if (idx < 0 || idx >= _current.answers.Count) continue;
                        var a = _current.answers[idx];
                        if (a == null) continue;
                        if (!string.IsNullOrWhiteSpace(a.explanation))
                            lines.Add(a.explanation.Trim());
                    }
                }

                // If nothing selected has an explanation, show explanations for correct answers.
                if (lines.Count == 0)
                {
                    for (int i = 0; i < _current.answers.Count; i++)
                    {
                        var a = _current.answers[i];
                        if (a == null || !a.isCorrect) continue;
                        if (!string.IsNullOrWhiteSpace(a.explanation))
                            lines.Add(a.explanation.Trim());
                    }
                }

                explanationText.text = lines.Count > 0 ? string.Join("\n\n", lines) : "";
            }
        }

        void ClearButtons()
        {
            for (int i = 0; i < _buttons.Count; i++)
                if (_buttons[i]) Destroy(_buttons[i].gameObject);
            _buttons.Clear();
        }
    }
}
