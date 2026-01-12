#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Interactables.Editor
{
    [CustomEditor(typeof(SelectionLists))]
    public class SelectionListsEditor : UnityEditor.Editor
    {
        // Serialized fields
        SerializedProperty _selectables, _lists, _feedback, _completeBtn, _retryBtn, _infoBtn;

        // Feedback
        SerializedProperty _textStart, _textPrompt, _textCorrect, _textWrong, _textRetry;

        // Colors
        SerializedProperty _useBtnColors, _colNormal, _colSelected, _colCompleted;

        // UI state
        static readonly Dictionary<int, bool> s_Foldout = new();
        static readonly Dictionary<int, Vector2> s_ListScroll = new();
        static string s_Filter = string.Empty;

        // Cached catalog from SelectablesManager
        struct CatalogCache { public int hash; public Collider[] items; public string[] names; }
        CatalogCache _cache;

        void OnEnable()
        {
            _selectables = serializedObject.FindProperty("selectables");
            _lists = serializedObject.FindProperty("lists");
            _feedback = serializedObject.FindProperty("feedback");
            _completeBtn = serializedObject.FindProperty("completeButton");
            _retryBtn = serializedObject.FindProperty("retryButton");
            _infoBtn = serializedObject.FindProperty("infoButton");

            _textStart = serializedObject.FindProperty("textStart");
            _textPrompt = serializedObject.FindProperty("textPrompt");
            _textCorrect = serializedObject.FindProperty("textCorrect");
            _textWrong = serializedObject.FindProperty("textWrong");
            _textRetry = serializedObject.FindProperty("textRetry");

            _useBtnColors = serializedObject.FindProperty("useButtonColors");
            _colNormal = serializedObject.FindProperty("buttonNormalColor");
            _colSelected = serializedObject.FindProperty("buttonSelectedColor");
            _colCompleted = serializedObject.FindProperty("buttonCompletedColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            HeaderHelp();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Catalog", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectables, new GUIContent("Selectables Manager"));

                var mgr = _selectables.objectReferenceValue as SelectablesManager;
                using (new EditorGUI.DisabledScope(mgr == null))
                {
                    EditorGUILayout.Space(2);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Refresh Catalog", GUILayout.Width(140)))
                            RefreshCatalog(mgr, force: true);

                        GUILayout.FlexibleSpace();
                        s_Filter = EditorGUILayout.TextField("Filter", s_Filter);
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Global UI", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_feedback, new GUIContent("Feedback (TMP_Text)"));
                EditorGUILayout.PropertyField(_completeBtn, new GUIContent("Complete Button"));
                EditorGUILayout.PropertyField(_retryBtn, new GUIContent("Retry Button"));
                EditorGUILayout.PropertyField(_infoBtn, new GUIContent("Info Button (optional)"));
                EditorGUILayout.LabelField(
                    "Tip: Assign an 'i' button here. For each list you can optionally assign an Info Panel; when the active list changes, panels are hidden until the button is pressed.",
                    EditorStyles.miniLabel);
            }

            // ----- Feedback Texts -----
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Feedback Texts", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("{list}, {correct}, {missed}, {extras} είναι διαθέσιμα placeholders.", MessageType.None);
                EditorGUILayout.PropertyField(_textStart, new GUIContent("Start"));
                EditorGUILayout.PropertyField(_textPrompt, new GUIContent("Prompt"));
                EditorGUILayout.PropertyField(_textCorrect, new GUIContent("Correct"));
                EditorGUILayout.PropertyField(_textWrong, new GUIContent("Wrong"));
                EditorGUILayout.PropertyField(_textRetry, new GUIContent("Retry"));
            }

            // ----- Button Colors -----
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Button Colors", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useBtnColors, new GUIContent("Use Button Colors"));
                if (_useBtnColors.boolValue)
                {
                    EditorGUILayout.PropertyField(_colNormal, new GUIContent("Normal"));
                    EditorGUILayout.PropertyField(_colSelected, new GUIContent("Selected (Active)"));
                    EditorGUILayout.PropertyField(_colCompleted, new GUIContent("Completed"));
                }
            }

            DrawLists();

            serializedObject.ApplyModifiedProperties();
        }

        // ---------------------------------------------------------------------

        void HeaderHelp()
        {
            EditorGUILayout.HelpBox(
                "Selection Lists (Controller)\n" +
                "• Λογική εδώ, όχι scripts πάνω στα selectable objects.\n" +
                "• Κάθε λίστα μπορεί να έχει world-space button & Animator.\n" +
                "• Οι 'Correct Items' τραβιούνται από τον Selectables Manager.\n" +
                "• Τα κουμπιά βάφονται (Normal/Selected/Completed) αν ενεργοποιήσεις το 'Use Button Colors'.",
                MessageType.Info);
        }

        void DrawLists()
        {
            var mgr = _selectables.objectReferenceValue as SelectablesManager;
            if (mgr != null)
                RefreshCatalog(mgr); // lazy cache

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Lists", EditorStyles.boldLabel);

                int removeAt = -1;

                for (int i = 0; i < _lists.arraySize; i++)
                {
                    var list = _lists.GetArrayElementAtIndex(i);

                    var name = list.FindPropertyRelative("name");
                    var correct = list.FindPropertyRelative("correct");
                    var btnRoot = list.FindPropertyRelative("buttonRoot");
                    var btnAnim = list.FindPropertyRelative("buttonAnimator");
                    var trigFwd = list.FindPropertyRelative("triggerComeForward");
                    var trigBack = list.FindPropertyRelative("triggerGoBack");
                    var trigDone = list.FindPropertyRelative("triggerCompleted");

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool open = s_Foldout.TryGetValue(i, out var v) ? v : false;
                        open = EditorGUILayout.Foldout(open, name.stringValue, true);
                        s_Foldout[i] = open;

                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Remove", GUILayout.Width(80)))
                            removeAt = i;
                    }

                    if (!s_Foldout[i]) continue;

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        name.stringValue = EditorGUILayout.TextField("Name", name.stringValue);

                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("UI (World-space)", EditorStyles.miniBoldLabel);
                        EditorGUILayout.PropertyField(btnRoot, new GUIContent("Button Root"));
                        EditorGUILayout.PropertyField(btnAnim, new GUIContent("Button Animator"));
                        var infoPanel = list.FindPropertyRelative("infoPanel");
                        EditorGUILayout.PropertyField(infoPanel, new GUIContent("Info Panel (optional)"));

                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("Animator Triggers", EditorStyles.miniBoldLabel);
                        EditorGUILayout.PropertyField(trigFwd, new GUIContent("Trigger • ComeForward"));
                        EditorGUILayout.PropertyField(trigBack, new GUIContent("Trigger • GoBack"));
                        EditorGUILayout.PropertyField(trigDone, new GUIContent("Trigger • Completed"));

                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Correct Items", EditorStyles.miniBoldLabel);

                        if (mgr == null || _cache.items == null || _cache.items.Length == 0)
                        {
                            EditorGUILayout.HelpBox(
                                "No colliders found in Selectables Manager. " +
                                "Άνοιξε τον manager και πάτα 'Collect From Children'.",
                                MessageType.None);
                        }
                        else
                        {
                            var items = FilteredCatalog();

                            if (!s_ListScroll.TryGetValue(i, out var scroll)) scroll = Vector2.zero;
                            using (var sv = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.MaxHeight(220)))
                            {
                                s_ListScroll[i] = sv.scrollPosition;

                                foreach (var idx in items)
                                {
                                    var col = _cache.items[idx];
                                    if (!col) continue;

                                    bool has = Contains(correct, col);
                                    bool now = EditorGUILayout.ToggleLeft(_cache.names[idx], has);
                                    if (now != has)
                                    {
                                        if (now) Add(correct, col);
                                        else Remove(correct, col);
                                    }
                                }
                            }

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button("Select All Catalog"))
                                {
                                    foreach (var idx in items)
                                    {
                                        var col = _cache.items[idx];
                                        if (col && !Contains(correct, col)) Add(correct, col);
                                    }
                                }
                                if (GUILayout.Button("Clear"))
                                    correct.ClearArray();
                            }
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add List"))
                    {
                        int idx = _lists.arraySize;
                        _lists.InsertArrayElementAtIndex(idx);
                        var nl = _lists.GetArrayElementAtIndex(idx);
                        nl.FindPropertyRelative("name").stringValue = $"List {idx + 1}";
                        nl.FindPropertyRelative("triggerComeForward").stringValue = "ComeForward";
                        nl.FindPropertyRelative("triggerGoBack").stringValue = "GoBack";
                        nl.FindPropertyRelative("triggerCompleted").stringValue = "Completed";
                        s_Foldout[idx] = true;
                    }
                    if (GUILayout.Button("Clear All"))
                        _lists.ClearArray();
                }

                if (removeAt >= 0) _lists.DeleteArrayElementAtIndex(removeAt);
            }
        }

        // ---------- Catalog cache / filter ----------

        void RefreshCatalog(SelectablesManager mgr, bool force = false)
        {
            if (mgr == null) { _cache = default; return; }

            int hash = ComputeCatalogHash(mgr);
            if (!force && hash == _cache.hash) return;

            var cols = mgr.items
                .Where(e => e != null && e.collider != null)
                .Select(e => e.collider)
                .ToArray();

            _cache = new CatalogCache
            {
                hash = hash,
                items = cols,
                names = cols.Select(c => c.name).ToArray()
            };
        }

        int ComputeCatalogHash(SelectablesManager mgr)
        {
            unchecked
            {
                int h = 17;
                var list = mgr.items ?? new List<SelectablesManager.Entry>();
                h = h * 31 + list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    var c = list[i]?.collider;
                    h = h * 31 + (c ? c.GetInstanceID() : 0);
                }
                return h;
            }
        }

        System.Collections.Generic.IEnumerable<int> FilteredCatalog()
        {
            if (_cache.items == null || _cache.items.Length == 0) yield break;

            if (string.IsNullOrEmpty(s_Filter))
            {
                for (int i = 0; i < _cache.items.Length; i++) yield return i;
                yield break;
            }

            string f = s_Filter.ToLowerInvariant();
            for (int i = 0; i < _cache.items.Length; i++)
                if (_cache.names[i] != null && _cache.names[i].ToLowerInvariant().Contains(f))
                    yield return i;
        }

        // ---------- SerializedProperty<Collider[]> helpers ----------

        static bool Contains(SerializedProperty list, Collider c)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                if (el.objectReferenceValue == c) return true;
            }
            return false;
        }

        static void Add(SerializedProperty list, Collider c)
        {
            int i = list.arraySize;
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = c;
        }

        static void Remove(SerializedProperty list, Collider c)
        {
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var el = list.GetArrayElementAtIndex(i);
                if (el.objectReferenceValue == c)
                {
                    list.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
        }
    }
}
#endif
