using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pitech.XR.Quiz
{
    [AddComponentMenu("Pi tech XR/Quiz/Quiz Results UI Controller")]
    public sealed class QuizResultsUIController : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Optional CanvasGroup used to show/hide without disabling the GameObject (recommended).")]
        public CanvasGroup canvasGroup;
        public TMP_Text titleText;
        public TMP_Text scoreText;
        public TMP_Text detailText;
        public Button continueButton;

        Action _onContinue;

        void Awake()
        {
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();

            if (continueButton)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(() =>
                {
                    var cb = _onContinue;
                    _onContinue = null;
                    cb?.Invoke();
                });
            }
        }

        public void Show(QuizAsset asset, QuizSession.QuizResult summary, Action onContinue)
        {
            _onContinue = onContinue;
            SetVisible(true);

            if (titleText)
                titleText.text = asset != null ? "Quiz Results" : "Results";

            float total = summary != null ? summary.totalScore : 0f;
            float max = summary != null ? summary.maxScore : 0f;
            float pct = max > 0f ? (total / max) : 0f;

            if (scoreText)
            {
                if (max > 0f)
                    scoreText.text = $"{total:0.#} / {max:0.#}  ({pct:P0})";
                else
                    scoreText.text = $"{total:0.#}";
            }

            if (detailText)
            {
                int correct = summary != null ? summary.correctCount : 0;
                int wrong = summary != null ? summary.wrongCount : 0;
                int answered = summary != null ? summary.answeredCount : 0;

                string passLine = "";
                if (asset != null && asset.passThresholdPercent > 0f)
                    passLine = (summary != null && summary.passed) ? "Passed" : "Failed";

                detailText.text =
                    $"Answered: {answered}\n" +
                    $"Correct: {correct}\n" +
                    $"Wrong: {wrong}" +
                    (!string.IsNullOrEmpty(passLine) ? $"\n{passLine}" : "");
            }
        }

        public void Hide()
        {
            _onContinue = null;
            SetVisible(false);
        }

        void SetVisible(bool on)
        {
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
    }
}


