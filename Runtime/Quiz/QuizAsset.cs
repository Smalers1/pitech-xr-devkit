using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Quiz
{
    [CreateAssetMenu(menuName = "Pi tech/Quiz Asset")]
    public sealed class QuizAsset : ScriptableObject
    {
        public enum QuestionType { SingleChoice, MultipleChoice }

        [Serializable]
        public sealed class Answer
        {
            public string text;
            public bool isCorrect;
            [TextArea] public string explanation;
        }

        [Serializable]
        public sealed class Question
        {
            [Tooltip("Stable identifier for this question (string).")]
            public string id;
            [TextArea] public string prompt;
            public QuestionType type = QuestionType.SingleChoice;
            [Min(0f)] public float points = 1f;
            public bool allowPartialCredit = false;
            public List<Answer> answers = new List<Answer>();
        }

        public List<Question> questions = new List<Question>();

        [Header("Feedback")]
        [Tooltip("If true, show correctness feedback immediately after answering.")]
        public bool showCorrectImmediately = true;

        [Tooltip("If true, show a summary at the end of the quiz.")]
        public bool showSummaryAtEnd = true;

        [Header("Passing")]
        [Tooltip("Pass threshold as a fraction of max score (0 = disabled).")]
        [Range(0f, 1f)] public float passThresholdPercent = 0f;

        public Question FindQuestion(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            string key = id.Trim();
            for (int i = 0; i < questions.Count; i++)
            {
                var q = questions[i];
                if (q != null && q.id == key) return q;
            }
            return null;
        }

        void OnValidate()
        {
            if (questions == null) return;
            for (int i = 0; i < questions.Count; i++)
            {
                var q = questions[i];
                if (q == null) continue;
                if (string.IsNullOrWhiteSpace(q.id))
                    q.id = Guid.NewGuid().ToString();
            }

            passThresholdPercent = Mathf.Clamp01(passThresholdPercent);
        }
    }
}
