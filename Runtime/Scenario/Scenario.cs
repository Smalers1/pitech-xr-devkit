﻿using System;
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

        // Keep the old Stats lists but hide them (won’t show in inspector)
        [HideInInspector] public List<StatEffect> onCorrectEffects = new();
        [HideInInspector] public List<StatEffect> onWrongEffects = new();

        public override string Kind => "Selection";
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

        void OnValidate()
        {
            if (steps == null) return;

            for (int i = steps.Count - 1; i >= 0; i--)
                if (steps[i] == null) steps.RemoveAt(i);

            foreach (var s in steps)
                if (s != null && string.IsNullOrEmpty(s.guid))
                    s.guid = Guid.NewGuid().ToString();
            if (!string.IsNullOrEmpty(title) && gameObject.name == "Scenario")
                gameObject.name = title;
        }

    }
}
