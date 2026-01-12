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
        public GameObject buttonRoot;     // optional (θα auto-wire Button/Graphic πάνω του)
        public Animator buttonAnimator;   // optional
        public TMP_Text title;            // optional

        [Header("Optional Info Panel")]
        [Tooltip("Optional panel (any UI) to show when the global Info button is pressed while this list is active.")]
        public GameObject infoPanel;

        [Header("Animator Trigger Names")]
        public string triggerComeForward = "ComeForward";
        public string triggerGoBack = "GoBack";
        public string triggerCompleted = "Completed";

        [NonSerialized] public bool isCompleted;

        // === Cache για scoring ===
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

        // === Cache για χρώματα κουμπιού ===
        [NonSerialized] public Button cachedButton;
        [NonSerialized] public Graphic cachedGraphic;    // Image, TMP_Text, οτιδήποτε παράγει χρώμα
        [NonSerialized] public Color originalColor;      // για επαναφορά σε Normal όταν δεν έχεις global normal
        [NonSerialized] public bool hasGraphic;

        public void CacheButtonGraphic()
        {
            cachedButton = null;
            cachedGraphic = null;
            hasGraphic = false;

            if (buttonRoot == null) return;

            buttonRoot.TryGetComponent(out cachedButton);
            if (cachedButton && cachedButton.targetGraphic)
            {
                cachedGraphic = cachedButton.targetGraphic;
            }
            else
            {
                // αλλιώς πιάσε οποιοδήποτε Graphic επάνω στο root
                buttonRoot.TryGetComponent(out cachedGraphic);
            }

            if (cachedGraphic)
            {
                hasGraphic = true;
                originalColor = cachedGraphic.color;
            }
        }
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
        public Button infoButton;      // optional: opens info panel for active list

        // ---------- Feedback Texts ----------
        [Header("Feedback Texts")]
        [TextArea] public string textStart = "Select a list to begin.";
        [TextArea] public string textPrompt = "Select the correct items for <b>{list}</b>, then press <b>Complete</b>.";
        [TextArea] public string textCorrect = "Correct! ({correct})";
        [TextArea] public string textWrong = "Not quite.\nMissed: {missed}  •  Extra: {extras}  •  Correct: {correct}\nPress Retry and try again.";
        [TextArea] public string textRetry = "Try again for <b>{list}</b> and press <b>Complete</b>.";

        // ---------- Button Colors ----------
        [Header("Button Colors (optional)")]
        [Tooltip("Αν είναι ενεργό, θα βάφει τα κουμπιά των λιστών ανάλογα με την κατάσταση.")]
        public bool useButtonColors = true;
        public Color buttonNormalColor = Color.white;
        public Color buttonSelectedColor = new Color(0.20f, 0.55f, 1f, 1f);  // μπλε-ish
        public Color buttonCompletedColor = new Color(0.20f, 0.80f, 0.25f, 1f); // πράσινο

        [SerializeField] int activeIndex = -1;
        public int Count => lists?.Count ?? 0;
        public int ActiveIndex => activeIndex;

        /// Raised whenever the active list selection changes (ή όταν αλλάξει active list).
        public event Action OnSelectionChanged;

        /// Convenience: πόσα σωστά έχει η ενεργή λίστα.
        public int ActiveTotalCorrect => (activeIndex >= 0 && activeIndex < Count && lists[activeIndex] != null)
            ? lists[activeIndex].CorrectCount : 0;

        void Awake()
        {
            // disable picking until a list is chosen
            if (selectables) selectables.pickingEnabled = false;

            // Set titles, auto-wire list buttons & cache button graphics
            for (int i = 0; i < Count; i++)
            {
                var l = lists[i];
                if (l == null) continue;
                l.RefreshLabel();

                // auto-wire
                if (l.buttonRoot && l.buttonRoot.TryGetComponent<Button>(out var b))
                {
                    int captured = i; // avoid closure pitfall
                    b.onClick.AddListener(() => ActivateList(captured));
                }

                // cache graphic for coloring
                l.CacheButtonGraphic();
            }

            // Wire global buttons if assigned
            if (completeButton) completeButton.onClick.AddListener(CompleteActive);
            if (retryButton) retryButton.onClick.AddListener(RetryActive);
            if (infoButton) infoButton.onClick.AddListener(ShowActiveInfo);

            ShowText(textStart);
            SetButtons(false, false);

            HideAllInfoPanels();
            UpdateInfoButtonState();

            // Βεβαιώσου ότι όλα τα κουμπιά είναι σε normal στην εκκίνηση
            RefreshButtonColors();
        }

        // ========= NEW: Scenario-facing helpers =========

        public int ShowList(string listName, bool reset = true)
        {
            for (int i = 0; i < Count; i++)
            {
                if (lists[i] != null && lists[i].name == listName)
                    return ShowList(i, reset);
            }
            return -1;
        }

        public int ShowList(int index, bool reset = true)
        {
            if (index < 0 || index >= Count || lists[index] == null)
                return -1;

            ActivateList(index);

            if (reset) ResetActive();
            else NotifySelectionChanged();

            return activeIndex;
        }

        public void ResetActive()
        {
            if (selectables)
            {
                selectables.ClearAll(alsoTurnOffHighlights: true);
                selectables.pickingEnabled = true;
            }
            NotifySelectionChanged();
        }

        // ----- Evaluation snapshot -----
        public struct Evaluation
        {
            public int totalCorrect;
            public int selectedTotal;
            public int selectedCorrect;
            public int selectedWrong;
            public bool allCorrectSelected;
        }

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

        public void NotifySelectionChanged() => OnSelectionChanged?.Invoke();

        // ============= Existing UI-driven flow =============

        public void ActivateList(int index)
        {
            if (index < 0 || index >= Count) return;

            // previous comes back (αν δεν ολοκληρώθηκε)
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

            // If there are per-list info panels, keep them hidden until explicitly opened.
            HideAllInfoPanels();

            if (selectables)
            {
                selectables.ClearAll(alsoTurnOffHighlights: true);
                selectables.pickingEnabled = true;
            }

            ShowText(textPrompt.Replace("{list}", cur.name));
            SetButtons(true, false);
            UpdateInfoButtonState();

            RefreshButtonColors();
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
                ShowText(textCorrect
                    .Replace("{correct}", correctSelected.ToString()));
                SetButtons(false, false);
            }
            else
            {
                ShowText(textWrong
                    .Replace("{missed}", missed.ToString())
                    .Replace("{extras}", extras.ToString())
                    .Replace("{correct}", correctSelected.ToString()));
                SetButtons(false, true);
            }

            RefreshButtonColors();
            UpdateInfoButtonState();
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
                ? textRetry.Replace("{list}", l.name)
                : textStart);

            SetButtons(true, false);
            HideAllInfoPanels();
            UpdateInfoButtonState();

            RefreshButtonColors();
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

        // ---------- Info panel ----------

        void UpdateInfoButtonState()
        {
            if (!infoButton) return;
            infoButton.interactable = HasActiveInfoPanel();
        }

        bool HasActiveInfoPanel()
        {
            if (activeIndex < 0 || activeIndex >= Count) return false;
            var l = lists[activeIndex];
            return l != null && l.infoPanel != null;
        }

        public void ShowActiveInfo()
        {
            if (activeIndex < 0 || activeIndex >= Count) return;
            var l = lists[activeIndex];
            if (l == null || l.infoPanel == null) return;

            HideAllInfoPanels();
            l.infoPanel.SetActive(true);
        }

        public void HideAllInfoPanels()
        {
            if (lists == null) return;
            for (int i = 0; i < lists.Count; i++)
            {
                var l = lists[i];
                if (l != null && l.infoPanel != null)
                    l.infoPanel.SetActive(false);
            }
        }

        // ---------- Button color logic ----------
        void RefreshButtonColors()
        {
            if (!useButtonColors || lists == null) return;

            for (int i = 0; i < lists.Count; i++)
            {
                var l = lists[i];
                if (l == null || !l.hasGraphic) continue;

                Color c;
                if (l.isCompleted)
                    c = buttonCompletedColor;
                else if (i == activeIndex)
                    c = buttonSelectedColor;
                else
                    c = buttonNormalColor;

                // βάψε targetGraphic (Image/TMP/etc.)
                l.cachedGraphic.color = c;

                // προαιρετικά μπορείς να πειράξεις και τα ButtonColors (Transition=ColorTint)
                if (l.cachedButton && l.cachedButton.transition == Selectable.Transition.ColorTint)
                {
                    var cb = l.cachedButton.colors;
                    cb.normalColor = buttonNormalColor;
                    cb.selectedColor = buttonSelectedColor;
                    cb.highlightedColor = buttonSelectedColor;
                    cb.pressedColor = buttonSelectedColor;
                    cb.disabledColor = new Color(cb.disabledColor.r, cb.disabledColor.g, cb.disabledColor.b, cb.disabledColor.a);
                    l.cachedButton.colors = cb;
                }
            }
        }
    }
}
