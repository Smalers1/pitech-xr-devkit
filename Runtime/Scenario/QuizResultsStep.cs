using System;
using UnityEngine;
using Pitech.XR.Quiz;

namespace Pitech.XR.Scenario
{
    /// <summary>
    /// Shows a quiz summary/results UI (score, correct/wrong, pass/fail) and waits for Continue.
    /// Use after a sequence of QuizStep questions.
    /// </summary>
    [Serializable]
    public sealed class QuizResultsStep : Step
    {
        [Header("Quiz Source")]
        [Tooltip("Optional quiz override. If empty, SceneManager.quiz is used.")]
        public QuizAsset quiz;

        public enum WhenComplete
        {
            AfterContinueButtonPressed,
            AfterSeconds
        }

        public enum CompleteMode
        {
            OnContinue,
            BranchOnPassed
        }

        [Header("When Complete")]
        public WhenComplete whenComplete = WhenComplete.AfterContinueButtonPressed;

        [Tooltip("Used only when When Complete is AfterSeconds.")]
        public float completeAfterSeconds = 2f;

        [Header("Completion")]
        public CompleteMode completion = CompleteMode.OnContinue;

        [Header("Routing")]
        [Tooltip("Next step (GUID) when completion is OnContinue. Empty = next item in list")]
        public string nextGuid = "";

        [Tooltip("Next step (GUID) when the quiz is passed (uses QuizAsset.passThresholdPercent).")]
        public string passedNextGuid = "";

        [Tooltip("Next step (GUID) when the quiz is failed (uses QuizAsset.passThresholdPercent).")]
        public string failedNextGuid = "";

        public override string Kind => "Quiz Results";
    }
}


