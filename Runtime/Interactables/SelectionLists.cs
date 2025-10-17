using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Pitech.XR.Interactables
{
    [Serializable]
    public class SelectionList
    {
        [Header("Identity")]
        public string name = "List";

        [Tooltip("Correct answers for this list (pick from SelectablesManager catalog).")]
        public List<Collider> correct = new();

        [Header("UI (World-space)")]
        public GameObject buttonRoot;     // optional (will auto-wire Button on it)
        public Animator buttonAnimator;   // optional
        public TMP_Text title;            // optional

        [Header("Animator Trigger Names")]
        public string triggerComeForward = "ComeForward";
        public string triggerGoBack = "GoBack";
        public string triggerCompleted = "Completed";

        [NonSerialized] public bool isCompleted;

        // cache for fast scoring
        [NonSerialized] HashSet<int> _ids;
        public void EnsureCache()
        {
            if (_ids != null) return;
            _ids = new HashSet<int>();
            foreach (var c in correct) if (c) _ids.Add(c.GetInstanceID());
        }
        public bool IsCorrect(int colliderInstanceId) { EnsureCache(); return _ids.Contains(colliderInstanceId); }
        public int CorrectCount { get { EnsureCache(); return _ids.Count; } }

        public void RefreshLabel() { if (title) title.text = name; }
        public void Fire(string trigger) { if (buttonAnimator && !string.IsNullOrEmpty(trigger)) buttonAnimator.SetTrigger(trigger); }
    }

    [AddComponentMenu("Pi tech XR/Interactables/Selection Lists (Controller)")]
    public class SelectionLists : MonoBehaviour
    {
        [Header("Catalog")]
        public SelectablesManager selectables;

        [Header("Lists")]
        public List<SelectionList> lists = new();

        [Header("Global UI")]
        public TMP_Text feedback;
        public Button completeButton;  // onClick wired automatically if present
        public Button retryButton;     // onClick wired automatically if present

        [SerializeField] int activeIndex = -1;
        public int Count => lists?.Count ?? 0;
        public int ActiveIndex => activeIndex;

        /// <summary>
        /// Raised whenever the active list selection changes (or active list changes).
        /// Call <see cref="NotifySelectionChanged"/> from your selectable toggle code if your SelectablesManager doesn't raise an event.
        /// </summary>
        public event Action OnSelectionChanged;

        /// <summary>Convenience: how many items are correct in the currently active list.</summary>
        public int ActiveTotalCorrect => (activeIndex >= 0 && activeIndex < Count && lists[activeIndex] != null)
            ? lists[activeIndex].CorrectCount : 0;

        void Awake()
        {
            // disable picking until a list is chosen
            if (selectables) selectables.pickingEnabled = false;

            // Set titles & auto-wire list buttons
            for (int i = 0; i < Count; i++)
            {
                var l = lists[i];
                if (l == null) continue;
                l.RefreshLabel();

                if (l.buttonRoot && l.buttonRoot.TryGetComponent<Button>(out var b))
                {
                    int captured = i; // avoid closure pitfall
                    b.onClick.AddListener(() => ActivateList(captured));
                }
            }

            // Wire global buttons if assigned
            if (completeButton) completeButton.onClick.AddListener(CompleteActive);
            if (retryButton) retryButton.onClick.AddListener(RetryActive);

            ShowText("Select a list to begin.");
            SetButtons(false, false);
        }

        // ========= NEW: Scenario-facing helpers =========

        /// <summary>Activate by list name. Returns the active index or -1 on failure.</summary>
        public int ShowList(string listName, bool reset = true)
        {
            for (int i = 0; i < Count; i++)
            {
                if (lists[i] != null && lists[i].name == listName)
                    return ShowList(i, reset);
            }
            return -1;
        }

        /// <summary>Activate by index. Returns the active index or -1 on failure.</summary>
        public int ShowList(int index, bool reset = true)
        {
            if (index < 0 || index >= Count || lists[index] == null)
                return -1;

            ActivateList(index);

            if (reset) ResetActive();
            else NotifySelectionChanged(); // still notify change so listeners know active list changed

            return activeIndex;
        }

        /// <summary>Clear the current selection for the active list.</summary>
        public void ResetActive()
        {
            if (selectables)
            {
                selectables.ClearAll(alsoTurnOffHighlights: true);
                selectables.pickingEnabled = true;
            }
            // notify
            NotifySelectionChanged();
        }

        /// <summary>Lightweight evaluation snapshot for the active list.</summary>
        public struct Evaluation
        {
            public int totalCorrect;     // how many items are correct in this list
            public int selectedTotal;    // how many currently selected (any)
            public int selectedCorrect;  // selected & correct
            public int selectedWrong;    // selected & not-correct
            public bool allCorrectSelected; // selectedCorrect == totalCorrect && selectedWrong == 0
        }

        /// <summary>Compute counts for the currently active list without mutating UI.</summary>
        public Evaluation EvaluateActive()
        {
            var e = new Evaluation();

            if (activeIndex < 0 || activeIndex >= Count) return e;
            var l = lists[activeIndex];
            if (l == null) return e;

            l.EnsureCache();

            var selected = (selectables != null) ? selectables.SelectedIds : Array.Empty<int>();
            e.totalCorrect = l.CorrectCount;

            foreach (var id in selected)
            {
                e.selectedTotal++;
                if (l.IsCorrect(id)) e.selectedCorrect++;
            }
            e.selectedWrong = e.selectedTotal - e.selectedCorrect;
            e.allCorrectSelected = (e.selectedCorrect == e.totalCorrect) && (e.selectedWrong == 0);
            return e;
        }

        /// <summary>
        /// Call this from your 3D selectable toggle/pick code whenever the selection changes.
        /// The Scenario runner listens to this for Auto-complete behavior.
        /// </summary>
        public void NotifySelectionChanged() => OnSelectionChanged?.Invoke();

        // ============= Existing UI-driven flow =============

        // Call from auto-wired or manual button
        public void ActivateList(int index)
        {
            if (index < 0 || index >= Count) return;

            // send previous (incomplete) back
            if (activeIndex >= 0 && activeIndex < Count)
            {
                var prev = lists[activeIndex];
                if (prev != null && !prev.isCompleted) prev.Fire(prev.triggerGoBack);
            }

            activeIndex = index;
            var cur = lists[activeIndex];
            if (cur == null) { ShowText("List data missing."); return; }

            cur.RefreshLabel();
            cur.Fire(cur.triggerComeForward);

            if (selectables)
            {
                selectables.ClearAll(alsoTurnOffHighlights: true);
                selectables.pickingEnabled = true;            // <<< enable selection
            }

            ShowText($"Select the correct items for <b>{cur.name}</b>, then press <b>Complete</b>.");
            SetButtons(true, false);

            // let any listeners know active list changed
            NotifySelectionChanged();
        }

        public void CompleteActive()
        {
            if (activeIndex < 0 || activeIndex >= Count) { ShowText("Pick a list first."); return; }
            var l = lists[activeIndex];
            if (l == null) { ShowText("List data missing."); return; }

            if (selectables) selectables.pickingEnabled = false;

            var selected = selectables != null ? selectables.SelectedIds : Array.Empty<int>();
            int correctSelected = 0, extras = 0;

            foreach (var id in selected)
                if (l.IsCorrect(id)) correctSelected++; else extras++;

            int missed = l.CorrectCount - correctSelected;
            bool allCorrect = (missed == 0 && extras == 0);

            if (allCorrect)
            {
                l.isCompleted = true;
                l.Fire(l.triggerCompleted);
                l.Fire(l.triggerGoBack);
                ShowText($"Correct! ({correctSelected})");
                SetButtons(false, false);
            }
            else
            {
                ShowText($"Not quite.\nMissed: {missed}  •  Extra: {extras}  •  Correct: {correctSelected}\nPress Retry and try again.");
                SetButtons(false, true);
            }

            // selections were evaluated; notify in case someone listens for completion attempts
            NotifySelectionChanged();
        }

        public void RetryActive()
        {
            if (activeIndex < 0 || activeIndex >= Count) return;

            if (selectables)
            {
                selectables.ClearAll(alsoTurnOffHighlights: true);
                selectables.pickingEnabled = true;
            }

            var l = lists[activeIndex];
            ShowText(l != null && !l.isCompleted
                ? $"Try again for <b>{l.name}</b> and press <b>Complete</b>."
                : "Select a list to begin.");

            SetButtons(true, false);

            // after clearing, selections changed
            NotifySelectionChanged();
        }

        public void ActivateListByName(string listName)
        {
            for (int i = 0; i < Count; i++)
                if (lists[i] != null && lists[i].name == listName) { ActivateList(i); return; }
        }

        void ShowText(string msg) { if (feedback) feedback.text = msg; }
        void SetButtons(bool canComplete, bool canRetry)
        {
            if (completeButton) completeButton.interactable = canComplete;
            if (retryButton) retryButton.interactable = canRetry;
        }
    }
}
