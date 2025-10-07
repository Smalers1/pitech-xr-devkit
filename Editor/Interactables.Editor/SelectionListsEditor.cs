#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Interactables.Editor
{
    [CustomEditor(typeof(SelectionLists))]
    public class SelectionListsEditor : UnityEditor.Editor
    {
        SerializedProperty _selectables, _lists, _feedback, _completeBtn, _retryBtn;

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

            EditorGUILayout.HelpBox(
                "Selection Lists (Controller)\n" +
                "• Logic lives here, no scripts on selectable objects.\n" +
                "• Each list can reference a world-space button root and Animator.\n" +
                "• The 'Correct Items' checklist is built from the Selectables Manager colliders.\n" +
                "• Hook your list buttons to SelectionLists.ActivateList(index).",
                MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Catalog", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_selectables, new GUIContent("Selectables Manager"));
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

        void DrawLists()
        {
            var mgr = _selectables.objectReferenceValue as SelectablesManager;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Lists", EditorStyles.boldLabel);

                if (!mgr)
                    EditorGUILayout.HelpBox("Assign a Selectables Manager to edit 'Correct Items'.", MessageType.Warning);

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

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            name.stringValue = EditorGUILayout.TextField("Name", name.stringValue);
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Remove", GUILayout.Width(80))) removeAt = i;
                        }

                        EditorGUILayout.PropertyField(btnRoot, new GUIContent("Button Root"));
                        EditorGUILayout.PropertyField(btnAnim, new GUIContent("Button Animator"));

                        // ==== simple vertical triggers ====
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Animator Triggers", EditorStyles.miniBoldLabel);
                        EditorGUILayout.PropertyField(trigFwd, new GUIContent("Trigger • ComeForward"));
                        EditorGUILayout.PropertyField(trigBack, new GUIContent("Trigger • GoBack"));
                        EditorGUILayout.PropertyField(trigDone, new GUIContent("Trigger • Completed"));
                        // ==================================

                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Correct Items", EditorStyles.miniBoldLabel);

                        if (mgr)
                        {
                            var entries = mgr.items
                                .Where(e => e != null && e.collider)
                                .Select(e => e.collider)
                                .ToList();

                            if (entries.Count == 0)
                            {
                                EditorGUILayout.HelpBox("No colliders found in Selectables Manager. Use 'Collect From Children' there.", MessageType.None);
                            }
                            else
                            {
                                foreach (var col in entries)
                                {
                                    bool has = Contains(correct, col);
                                    bool now = EditorGUILayout.ToggleLeft(col.name, has);
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
                                    foreach (var col in entries)
                                        if (!Contains(correct, col)) Add(correct, col);

                                if (GUILayout.Button("Clear"))
                                    correct.ClearArray();
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("Correct Items hidden — assign a Selectables Manager.", MessageType.None);
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
                    }
                    if (GUILayout.Button("Clear All"))
                        _lists.ClearArray();
                }

                if (removeAt >= 0) _lists.DeleteArrayElementAtIndex(removeAt);
            }
        }

        // helpers for collider list
        static bool Contains(SerializedProperty list, Collider c)
        {
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == c) return true;
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
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == c)
                {
                    list.DeleteArrayElementAtIndex(i);
                    break;
                }
        }
    }
}
#endif
