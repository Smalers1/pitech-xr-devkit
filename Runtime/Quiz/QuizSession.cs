using System;
using System.Collections.Generic;
using System.Linq;

namespace Pitech.XR.Quiz
{
    public sealed class QuizSession
    {
        public sealed class QuestionResult
        {
            public string questionId;
            public List<int> selected = new List<int>();
            public bool isCorrect;
            public float score;
            public float maxScore;
        }

        public sealed class QuizResult
        {
            public float totalScore;
            public float maxScore;
            public int correctCount;
            public int wrongCount;
            public int answeredCount;
            public bool passed;
        }

        public QuizAsset Asset { get; private set; }

        readonly Dictionary<string, QuestionResult> _results = new Dictionary<string, QuestionResult>(StringComparer.Ordinal);

        public event Action<QuestionResult> OnQuestionAnswered;
        public event Action<QuizResult> OnQuizCompleted;

        public QuizSession(QuizAsset asset)
        {
            Asset = asset;
        }

        public void SetAsset(QuizAsset asset)
        {
            if (Asset == asset) return;
            Asset = asset;
            _results.Clear();
        }

        public QuestionResult AnswerQuestion(QuizAsset.Question q, IList<int> selected)
        {
            if (q == null) return null;

            var result = new QuestionResult
            {
                questionId = q.id,
                selected = selected != null ? new List<int>(selected) : new List<int>(),
                maxScore = q.points
            };

            Evaluate(q, result);
            _results[q.id] = result;
            OnQuestionAnswered?.Invoke(result);
            return result;
        }

        public QuizResult BuildSummary(bool invokeEvent = true)
        {
            var summary = new QuizResult();
            foreach (var kv in _results)
            {
                var r = kv.Value;
                if (r == null) continue;
                summary.answeredCount++;
                summary.totalScore += r.score;
                summary.maxScore += r.maxScore;
                if (r.isCorrect) summary.correctCount++;
                else summary.wrongCount++;
            }
            if (Asset != null && summary.maxScore > 0f && Asset.passThresholdPercent > 0f)
                summary.passed = summary.totalScore >= summary.maxScore * Asset.passThresholdPercent;
            if (invokeEvent)
                OnQuizCompleted?.Invoke(summary);
            return summary;
        }

        void Evaluate(QuizAsset.Question q, QuestionResult r)
        {
            if (q.answers == null || q.answers.Count == 0)
            {
                r.isCorrect = false;
                r.score = 0f;
                return;
            }

            var correctIndices = new HashSet<int>();
            for (int i = 0; i < q.answers.Count; i++)
                if (q.answers[i] != null && q.answers[i].isCorrect)
                    correctIndices.Add(i);

            var selectedSet = new HashSet<int>(r.selected ?? new List<int>());
            bool anyWrong = selectedSet.Any(i => !correctIndices.Contains(i));
            int correctSelected = selectedSet.Count(i => correctIndices.Contains(i));
            int totalCorrect = correctIndices.Count;

            if (q.type == QuizAsset.QuestionType.SingleChoice)
            {
                r.isCorrect = !anyWrong && correctSelected == 1 && totalCorrect == 1;
                r.score = r.isCorrect ? q.points : 0f;
                return;
            }

            // Multiple choice
            if (anyWrong)
            {
                r.isCorrect = false;
                r.score = 0f;
                return;
            }

            if (correctSelected == totalCorrect)
            {
                r.isCorrect = true;
                r.score = q.points;
                return;
            }

            r.isCorrect = false;
            if (q.allowPartialCredit && totalCorrect > 0)
            {
                float ratio = correctSelected / (float)totalCorrect;
                r.score = q.points * ratio;
            }
            else
            {
                r.score = 0f;
            }
        }
    }
}
