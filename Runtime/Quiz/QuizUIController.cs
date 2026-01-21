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
        [Header("UI")]
        public TMP_Text promptText;
        public Transform answersRoot;
        public Button answerButtonPrefab;
        public Button submitButton;

        readonly List<Button> _buttons = new List<Button>();
        readonly HashSet<int> _selected = new HashSet<int>();
        Action<List<int>> _onAnswered;
        QuizAsset.Question _current;

        public void ShowQuestion(QuizAsset.Question q, Action<List<int>> onAnswered)
        {
            if (q == null) return;

            _current = q;
            _onAnswered = onAnswered;
            _selected.Clear();

            if (promptText) promptText.text = q.prompt;

            RebuildButtons(q);
            SetSubmitVisible(q.type == QuizAsset.QuestionType.MultipleChoice);
        }

        public void Hide()
        {
            if (promptText) promptText.text = "";
            ClearButtons();
            SetSubmitVisible(false);
            _current = null;
            _onAnswered = null;
            _selected.Clear();
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
                submitButton.onClick.AddListener(SubmitMulti);
            }
        }

        void OnAnswerClicked(int idx)
        {
            if (_current == null) return;

            if (_current.type == QuizAsset.QuestionType.SingleChoice)
            {
                _selected.Clear();
                _selected.Add(idx);
                SubmitNow();
            }
            else
            {
                if (_selected.Contains(idx)) _selected.Remove(idx);
                else _selected.Add(idx);
                RefreshButtonStates();
            }
        }

        void SubmitMulti()
        {
            if (_current == null) return;
            if (_current.type != QuizAsset.QuestionType.MultipleChoice) return;
            SubmitNow();
        }

        void SubmitNow()
        {
            var list = new List<int>(_selected);
            _onAnswered?.Invoke(list);
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

        void SetSubmitVisible(bool on)
        {
            if (submitButton) submitButton.gameObject.SetActive(on);
        }

        void ClearButtons()
        {
            for (int i = 0; i < _buttons.Count; i++)
                if (_buttons[i]) Destroy(_buttons[i].gameObject);
            _buttons.Clear();
        }
    }
}
