using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Pitech.XR.Stats;
using UnityEngine.UI;
using Pitech.XR.Interactables;
using UnityEngine.Events;

namespace Pitech.XR.Scenario
{
    // ---------- Base data ----------
    [Serializable]
    public abstract class Step
    {
        public string guid;          // used by the graph to connect steps
        public Vector2 graphPos;     // node position in the graph
        protected Step() { guid = Guid.NewGuid().ToString(); }
        public abstract string Kind { get; }
    }

    [Serializable]
    public class TimelineStep : Step
    {
        [Tooltip("Director in the scene that already has the PlayableAsset + bindings")]
        public PlayableDirector director;
        public bool rewindOnEnter = true;
        public bool waitForEnd = true;

        [Tooltip("Next step (GUID). Empty = next item in list")]
        public string nextGuid = "";

        public override string Kind => "Timeline";
    }

    [Serializable]
    public class CueCardsStep : Step
    {
        [Header("Timeline sync (optional)")]
        [Tooltip("Optional clock. If empty we use a local stopwatch.")]
        public PlayableDirector director;

        [Header("Cards in order")]
        [Tooltip("UI objects for each card (order matters)")]
        public GameObject[] cards;

        [Tooltip("Cue Times (sec) = max seconds each card stays before auto-advance if player doesn’t tap. " +
                 "Length can be 1 (applies to all) or match the number of cards. Leave empty for tap-only.")]
        public float[] cueTimes;

        [Header("Behavior")]
        public bool autoShowFirst = true;
        public GameObject tapHint;

        [Header("Optional extra object")]
        public GameObject extraObject;
        public int extraShowAtIndex = 1;
        public bool hideExtraWithFinalTap = true;
        public bool useRenderersForExtra = true;

        [Header("Transitions")]
        public float fadeDuration = 0.25f;
        public float popScale = 1.06f;
        public float popDuration = 0.18f;
        public AnimationCurve fadeCurve = null;   // null = EaseInOut
        public AnimationCurve scaleCurve = null;  // null = EaseInOut

        [Tooltip("Next step (GUID). Empty = next item in list")]
        public string nextGuid = "";

        public override string Kind => "Cue Cards";
    }

    [Serializable]
    public class Choice
    {
        [Tooltip("UGUI Button in your panel")]
        public UnityEngine.UI.Button button;

        [Header("Events")]
        [Tooltip("Invoked when this choice is selected (button pressed).")]
        public UnityEvent onSelected = new UnityEvent();

        [Tooltip("Stat changes when this is pressed")]
        public List<StatEffect> effects = new();

        [Tooltip("Next step (GUID) if this choice is picked. Empty = next item in list")]
        public string nextGuid = "";
    }

    [Serializable]
    public class QuestionStep : Step
    {
        [Header("Panel")]
        public RectTransform panelRoot;
        public Animator panelAnimator;
        public string showTrigger = "Show";
        public string hideTrigger = "Hide";
        public float fallbackHideSeconds = 50f;

        [Header("Choices")]
        public List<Choice> choices = new();

        public override string Kind => "Question";
    }

    // ---------- Mini Quiz (multiple questions on one panel; routes by score) ----------
    [Serializable]
    public class MiniQuizChoice
    {
        [Tooltip("UGUI Button for this answer option.")]
        public UnityEngine.UI.Button button;

        [Tooltip("If true, this selection counts as correct for the question.")]
        public bool isCorrect = false;

        [Header("Events")]
        [Tooltip("Invoked when this option is selected.")]
        public UnityEvent onSelected = new UnityEvent();

        [Header("Stat Effects")]
        [Tooltip("Optional stat changes when this option is selected.")]
        public List<StatEffect> effects = new();
    }

    [Serializable]
    public class MiniQuizQuestion
    {
        [Tooltip("Optional label for editor clarity (not shown automatically in UI).")]
        public string label;

        [Tooltip("Answer options for this question. Typically 2 (Yes/No) or more.")]
        public List<MiniQuizChoice> choices = new();
    }

    [Serializable]
    public class MiniQuizOutcome
    {
        [Tooltip("Optional label shown on the ScenarioGraph port.")]
        public string label;

        [Min(0)]
        [Tooltip("Minimum correct answers (inclusive) for this outcome.")]
        public int minCorrect = 0;

        [Tooltip("Maximum correct answers (inclusive). Use -1 for no maximum.")]
        public int maxCorrect = -1;

        [Tooltip("Next step (GUID) if score matches this outcome. Empty = next item in list.")]
        public string nextGuid = "";
    }

    [Serializable]
    public class MiniQuizStep : Step
    {
        [Header("Panel")]
        public RectTransform panelRoot;
        public Animator panelAnimator;
        public string showTrigger = "Show";
        public string hideTrigger = "Hide";

        [Header("Questions")]
        public List<MiniQuizQuestion> questions = new();

        public enum CompleteMode
        {
            AutoWhenAllAnswered,
            OnSubmitButton
        }

        [Header("Completion")]
        public CompleteMode completion = CompleteMode.AutoWhenAllAnswered;

        [Tooltip("Optional submit/complete button when completion == OnSubmitButton.")]
        public Button submitButton;

        [Tooltip("If true, once a question is answered its choice buttons are disabled.")]
        public bool lockQuestionAfterAnswer = true;

        [Header("Routing (by correct count)")]
        [Tooltip("Evaluate score (correct answers) and route to the first matching outcome.")]
        public List<MiniQuizOutcome> outcomes = new();

        [Tooltip("Fallback next step if no outcome matches. Empty = next item in list.")]
        public string defaultNextGuid = "";

        public override string Kind => "Mini Quiz";
    }

    [Serializable]
    public sealed class SelectionStep : Step
    {
        [Header("Source")]
        [Tooltip("Scene reference to the SelectionLists controller handling all selection lists in this scene.")]
        public SelectionLists lists;

        [Tooltip("Which list to test on. You can use either List Name or List Index.")]
        public string listKey;
        public int listIndex = -1;

        [Header("Flow")]
        [Tooltip("Reset/clear selections when this step begins.")]
        public bool resetOnEnter = true;

        public enum CompleteMode
        {
            AutoWhenRequirementMet,   // auto-advance once requirement is met
            OnSubmitButton            // wait until a submit click
        }
        public CompleteMode completion = CompleteMode.AutoWhenRequirementMet;

        [Tooltip("Optional submit button. Used only if completion == OnSubmitButton.")]
        public Button submitButton;

        [Header("Requirement")]
        [Min(0)] public int requiredSelections = 1;
        [Tooltip("If true, user must select exactly 'requiredSelections'. If false, 'at least' that many.")]
        public bool requireExactCount = false;

        [Tooltip("How many wrong selections are tolerated and still considered overall correct.")]
        [Min(0)] public int allowedWrong = 0;

        [Tooltip("Optional timeout (seconds). If > 0 and time elapses before completion, the step resolves as WRONG.")]
        [Min(0)] public float timeoutSeconds = 0f;

        [Header("Routing")]
        [Tooltip("Next step (GUID) when evaluation is CORRECT. If empty, runner may fall back to linear next.")]
        public string correctNextGuid = "";
        [Tooltip("Next step (GUID) when evaluation is WRONG. If empty, runner may fall back to linear next.")]
        public string wrongNextGuid = "";

        [Header("Optional UI")]
        public RectTransform panelRoot;
        public Animator panelAnimator;
        public string showTrigger = "Show";
        public string hideTrigger = "Hide";
        public GameObject hint;

        [Header("Events")]
        public UnityEvent onCorrect = new UnityEvent();
        public UnityEvent onWrong = new UnityEvent();

        [Header("Stat Effects")]
        public List<StatEffect> onCorrectEffects = new();
        public List<StatEffect> onWrongEffects = new();

        public override string Kind => "Selection";
    }

    // -------- InsertStep --------
    [Serializable]
    public sealed class InsertStep : Step
    {
        [Header("Object to insert")]
        [Tooltip("Root object of the tool / item the learner moves. Rigidbody can be on this or a child.")]
        public Transform item;

        [Header("Target slot")]
        [Tooltip("Trigger collider that represents the slot. Used for proximity/containment checks.")]
        public Collider targetTrigger;

        [Tooltip("Final pose for the inserted object. If empty we use targetTrigger.transform.")]
        public Transform attachTransform;

        [Header("Attach behaviour")]
        [Tooltip("If true, once 'inserted' the object is smoothly moved & rotated into the final pose.")]
        public bool smoothAttach = true;

        [Tooltip("Parent the item to the attachTransform on completion.")]
        public bool parentToAttach = true;

        [Tooltip("Movement speed when auto-attaching (m/s).")]
        public float moveSpeed = 5f;

        [Tooltip("Rotation speed when auto-attaching (lerp factor per second).")]
        public float rotateSpeed = 5f;

        [Header("Detection")]
        [Tooltip("How close (meters) to the attachTransform before we consider the object 'inserted'.")]
        public float positionTolerance = 0.02f;

        [Tooltip("How close (degrees) in rotation. Set 0 to ignore rotation for completion.")]
        public float angleTolerance = 10f;

        [Header("Routing")]
        [Tooltip("Next step (GUID). Empty = next item in list")]
        public string nextGuid = "";

        public override string Kind => "Insert";
    }

    [Serializable]
    public sealed class EventStep : Step
    {
        [Header("Events")]
        [Tooltip("Invoked when this step starts")]
        public UnityEngine.Events.UnityEvent onEnter = new UnityEngine.Events.UnityEvent();

        [Header("Flow")]
        [Tooltip("Optional delay before we advance to the next step (seconds). 0 = advance immediately")]
        public float waitSeconds = 0f;

        [Tooltip("Next step (GUID). Empty = next item in list")]
        public string nextGuid = "";

        public override string Kind => "Event";
    }

    // -------- GroupStep --------
    /// <summary>
    /// Runs multiple nested steps concurrently (optional advanced authoring).
    /// Group has a single input and output (nextGuid).
    /// </summary>
    [Serializable]
    public sealed class GroupStep : Step
    {
        public enum CompleteWhen
        {
            AllChildrenComplete,
            AnyChildCompletes,
            SpecificChildCompletes,
            RequiredChildrenComplete,
            NOfMChildrenComplete,
        }

        [Header("Group Steps")]
        [Tooltip("Nested steps that run together. Their routing fields are ignored; the Group controls routing via nextGuid.")]
        [SerializeReference] public List<Step> steps = new();

        [Header("Completion")]
        public CompleteWhen completeWhen = CompleteWhen.AllChildrenComplete;

        [Tooltip("Used only when CompleteWhen == NOfMChildrenComplete.")]
        [Min(1)] public int requiredCount = 1;

        [Tooltip("Used only when CompleteWhen == SpecificChildCompletes. Must match a nested step guid.")]
        public string specificStepGuid = "";

        [Tooltip("If true, when the group completes early (timer/specific-step), other running steps will be stopped/cleaned up.")]
        public bool stopOthersOnComplete = true;

        [Serializable]
        public class ChildRequirement
        {
            public string guid;
            public bool required = true;
        }

        [Tooltip("Optional required flags per child (used by Required/N-of-M completion modes).")]
        public List<ChildRequirement> childRequirements = new();

        [Header("Routing")]
        [Tooltip("Next step (GUID). Empty = next item in list")]
        public string nextGuid = "";

        public override string Kind => "Group";

        public void EnsureChildRequirements()
        {
            if (steps == null) return;
            if (childRequirements == null) childRequirements = new List<ChildRequirement>();

            var existing = new HashSet<string>();
            for (int i = 0; i < steps.Count; i++)
            {
                var st = steps[i];
                if (st == null) continue;
                if (string.IsNullOrEmpty(st.guid)) st.guid = Guid.NewGuid().ToString();
                existing.Add(st.guid);

                bool found = false;
                for (int k = 0; k < childRequirements.Count; k++)
                {
                    if (childRequirements[k] != null && childRequirements[k].guid == st.guid)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    childRequirements.Add(new ChildRequirement { guid = st.guid, required = true });
            }

            for (int i = childRequirements.Count - 1; i >= 0; i--)
            {
                var c = childRequirements[i];
                if (c == null || string.IsNullOrEmpty(c.guid) || !existing.Contains(c.guid))
                    childRequirements.RemoveAt(i);
            }
        }

        public bool IsChildRequired(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return true;
            if (childRequirements == null || childRequirements.Count == 0) return true;
            for (int i = 0; i < childRequirements.Count; i++)
                if (childRequirements[i] != null && childRequirements[i].guid == guid)
                    return childRequirements[i].required;
            return true;
        }
    }


    // ---------- Holder on the scene ----------
    [DisallowMultipleComponent]
    public class Scenario : MonoBehaviour
    {
        // human–friendly name for this scenario (used in inspectors, logs, dashboards)
        [SerializeField, Tooltip("Human-friendly name for this scenario")]
        private string title = "Main Scenario";
        public string Title => title;

        [SerializeReference] public List<Step> steps = new();

#if UNITY_EDITOR
        // Editor-only graph notes (saved with the scene on the Scenario component).
        [Serializable]
        public sealed class GraphNote
        {
            public string guid;
            public Rect rect = new Rect(80, 80, 240, 160);
            [TextArea] public string text = "Note…";
        }

        [SerializeField] List<GraphNote> graphNotes = new();
        public List<GraphNote> GraphNotes => graphNotes;
#endif

        void OnValidate()
        {
            if (steps == null) return;

            for (int i = steps.Count - 1; i >= 0; i--)
                if (steps[i] == null) steps.RemoveAt(i);

            EnsureGuidsRecursive(steps);

            if (!string.IsNullOrEmpty(title) && gameObject.name == "Scenario")
                gameObject.name = title;
        }

        static void EnsureGuidsRecursive(List<Step> list)
        {
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null) continue;

                if (string.IsNullOrEmpty(s.guid))
                    s.guid = Guid.NewGuid().ToString();

                if (s is GroupStep g && g.steps != null)
                {
                    // Clean nulls in nested list as well
                    for (int k = g.steps.Count - 1; k >= 0; k--)
                        if (g.steps[k] == null) g.steps.RemoveAt(k);

                    EnsureGuidsRecursive(g.steps);
                    g.EnsureChildRequirements();
                }
            }
        }
    }
}
