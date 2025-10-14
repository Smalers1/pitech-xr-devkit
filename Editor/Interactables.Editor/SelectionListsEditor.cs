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
        SerializedProperty _selectables, _lists, _feedback, _completeBtn, _retryBtn;

        // UI state (static so it persists while selecting other objects)
        static readonly Dictionary<int, bool> s_Foldout = new();        // listIndex -> open?
        static readonly Dictionary<int, Vector2> s_ListScroll = new();  // listIndex -> scroll
        static string s_Filter = string.Empty;

        // Cached catalog from SelectablesManager (to avoid rebuilding each repaint)
        struct CatalogCache
        {
            public int hash;
            public Collider[] items;
            public string[] names;
        }
        CatalogCache _cache;

        void OnEnable()
        {
            _selectables = serializedObject.FindProperty("selectables");
            _lists = serializedObject.FindProperty("lists");
            _feedback = serializedObject.FindProperty("feedback");
            _completeBtn = serializedObject.FindProperty("completeButton");
            _retryBtn = serializedObject.FindProperty("retryButton");
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
            }

            DrawLists();

            serializedObject.ApplyModifiedProperties();
        }

        // ---------------------------------------------------------------------
        // Blocks
        // ---------------------------------------------------------------------

        void HeaderHelp()
        {
            EditorGUILayout.HelpBox(
                "Selection Lists (Controller)\n" +
                "• Logic lives here, no scripts on selectable objects.\n" +
                "• Each list can reference a world-space button root and Animator.\n" +
                "• The 'Correct Items' checklist pulls from the Selectables Manager colliders.\n" +
                "• Hook list buttons to SelectionLists.ActivateList(index).",
                MessageType.Info);
        }

        void DrawLists()
        {
            var mgr = _selectables.objectReferenceValue as SelectablesManager;
            if (mgr != null)
                RefreshCatalog(mgr); // lazy/no-op if unchanged

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

                    // Foldout header row
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool open = s_Foldout.TryGetValue(i, out var v) ? v : false;
                        open = EditorGUILayout.Foldout(open, name.stringValue, true);
                        s_Foldout[i] = open;

                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Remove", GUILayout.Width(80)))
                            removeAt = i;
                    }

                    if (!s_Foldout[i]) continue; // collapsed: draw nothing else

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        // Name
                        name.stringValue = EditorGUILayout.TextField("Name", name.stringValue);

                        // World-space UI refs
                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("UI (World-space)", EditorStyles.miniBoldLabel);
                        EditorGUILayout.PropertyField(btnRoot, new GUIContent("Button Root"));
                        EditorGUILayout.PropertyField(btnAnim, new GUIContent("Button Animator"));

                        // Triggers (vertical order)
                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("Animator Triggers", EditorStyles.miniBoldLabel);
                        EditorGUILayout.PropertyField(trigFwd, new GUIContent("Trigger • ComeForward"));
                        EditorGUILayout.PropertyField(trigBack, new GUIContent("Trigger • GoBack"));
                        EditorGUILayout.PropertyField(trigDone, new GUIContent("Trigger • Completed"));

                        // Correct items
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Correct Items", EditorStyles.miniBoldLabel);

                        if (mgr == null || _cache.items == null || _cache.items.Length == 0)
                        {
                            EditorGUILayout.HelpBox(
                                "No colliders found in Selectables Manager. " +
                                "Open the manager and use 'Collect From Children'.",
                                MessageType.None);
                        }
                        else
                        {
                            // Filtered view
                            var items = FilteredCatalog();

                            // Scroll (per list)
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
                        s_Foldout[idx] = true; // open the new one
                    }
                    if (GUILayout.Button("Clear All"))
                        _lists.ClearArray();
                }

                if (removeAt >= 0) _lists.DeleteArrayElementAtIndex(removeAt);
            }
        }

        // ---------------------------------------------------------------------
        // Catalog caching / filtering
        // ---------------------------------------------------------------------

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

        IEnumerable<int> FilteredCatalog()
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

        // ---------------------------------------------------------------------
        // List helpers for the SerializedProperty<Collider[]>
        // ---------------------------------------------------------------------

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
