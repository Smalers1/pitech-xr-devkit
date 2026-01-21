using System;
using UnityEngine;
using Pitech.XR.Quiz;

namespace Pitech.XR.Scenario
{
    [Serializable]
    public sealed class QuizStep : Step
    {
        public enum CompleteMode
        {
            AnyAnswer,          // advance regardless of correctness
            BranchOnCorrectness // route to Correct/Wrong outputs
        }

        public enum AnswerSubmitMode
        {
            ImmediateSelection, // selecting an answer completes immediately (SingleChoice); MultiChoice still requires Submit
            OnSubmitButton      // selecting an answer only highlights; user must press Submit
        }

        public enum FeedbackMode
        {
            None,
            ForSeconds,
            UntilContinue
        }

        [Header("Quiz Source")]
        [Tooltip("Optional quiz override. If empty, SceneManager.defaultQuiz is used.")]
        public QuizAsset quiz;

        [Tooltip("Stable Question ID (preferred).")]
        public string questionId;

        [Tooltip("Fallback index if Question ID is empty or not found.")]
        public int questionIndex = -1;

        [Header("Answering")]
        public AnswerSubmitMode submitMode = AnswerSubmitMode.ImmediateSelection;

        [Header("After Answer")]
        [Tooltip("Optional feedback/explanation stage before advancing.")]
        public FeedbackMode feedback = FeedbackMode.None;

        [Tooltip("Used only when Feedback == ForSeconds.")]
        [Min(0f)] public float feedbackSeconds = 2f;

        [Header("Completion")]
        public CompleteMode completion = CompleteMode.AnyAnswer;

        [Header("Routing")]
        [Tooltip("Next step (GUID) when completion is AnyAnswer. Empty = next item in list")]
        public string nextGuid = "";

        [Tooltip("Next step (GUID) when the answer is correct.")]
        public string correctNextGuid = "";

        [Tooltip("Next step (GUID) when the answer is wrong.")]
        public string wrongNextGuid = "";

        public override string Kind => "Quiz";
    }
}
