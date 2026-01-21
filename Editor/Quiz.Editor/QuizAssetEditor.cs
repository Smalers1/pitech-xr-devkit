#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Pitech.XR.Quiz;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Pitech.XR.Quiz.Editor
{
    [CustomEditor(typeof(QuizAsset))]
    public sealed class QuizAssetEditor : UnityEditor.Editor
    {
        SerializedProperty _questionsProp;
        ReorderableList _questionsList;
        readonly Dictionary<int, ReorderableList> _answersLists = new Dictionary<int, ReorderableList>();
        SerializedProperty _showCorrectProp;
        SerializedProperty _showSummaryProp;
        SerializedProperty _passThresholdProp;

        static bool _foldQuestions = true;
        static bool _showIds;
        static readonly Dictionary<int, bool> _expanded = new Dictionary<int, bool>();
        static readonly Dictionary<string, bool> _showExplanation = new Dictionary<string, bool>();

        void OnEnable()
        {
            _questionsProp = serializedObject.FindProperty("questions");
            _showCorrectProp = serializedObject.FindProperty("showCorrectImmediately");
            _showSummaryProp = serializedObject.FindProperty("showSummaryAtEnd");
            _passThresholdProp = serializedObject.FindProperty("passThresholdPercent");

            _questionsList = new ReorderableList(serializedObject, _questionsProp, true, true, true, true);
            _questionsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Questions");
            _questionsList.elementHeightCallback = GetQuestionHeight;
            _questionsList.drawElementCallback = DrawQuestionElement;
            _questionsList.drawElementBackgroundCallback = (r, i, a, f) =>
            {
                if (Event.current.type == EventType.Repaint)
                {
                    var c = (i % 2 == 0) ? Styles.RowEven : Styles.RowOdd;
                    EditorGUI.DrawRect(r, c);
                }
            };
            _questionsList.onAddCallback = _ =>
            {
                int idx = _questionsProp.arraySize;
                _questionsProp.InsertArrayElementAtIndex(idx);
                var q = _questionsProp.GetArrayElementAtIndex(idx);
                ResetQuestion(q);
                _expanded[idx] = true;
                serializedObject.ApplyModifiedProperties();
            };
        }

        public override void OnInspectorGUI()
        {
            Styles.Ensure();
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(6);

            Styles.Section("Quiz Settings", true, () =>
            {
                if (_showCorrectProp != null)
                    EditorGUILayout.PropertyField(_showCorrectProp, new GUIContent("Show Correct Immediately"));
                if (_showSummaryProp != null)
                    EditorGUILayout.PropertyField(_showSummaryProp, new GUIContent("Show Summary At End"));
                if (_passThresholdProp != null)
                    EditorGUILayout.Slider(_passThresholdProp, 0f, 1f, new GUIContent("Pass Threshold (%)"));
            });

            _foldQuestions = Styles.Section("Questions", _foldQuestions, () =>
            {
                _questionsList.DoLayoutList();
            });

            EditorGUILayout.Space(6);
            DrawValidation();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(Styles.InfoBox))
            {
                EditorGUILayout.LabelField("Quiz Asset", Styles.HeaderTitle);
                EditorGUILayout.LabelField("Author questions, answers, and scoring in one place.", Styles.Muted);

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Question", Styles.Primary, GUILayout.Height(22)))
                    {
                        int idx = _questionsProp.arraySize;
                        _questionsProp.InsertArrayElementAtIndex(idx);
                        var q = _questionsProp.GetArrayElementAtIndex(idx);
                        ResetQuestion(q);
                        _expanded[idx] = true;
                        serializedObject.ApplyModifiedProperties();
                    }

                    if (GUILayout.Button(_showIds ? "Hide IDs" : "Show IDs", Styles.Mid, GUILayout.Height(22)))
                        _showIds = !_showIds;

                    if (GUILayout.Button("Expand All", Styles.Mid, GUILayout.Height(22)))
                        SetAllExpanded(true);

                    if (GUILayout.Button("Collapse All", Styles.Mid, GUILayout.Height(22)))
                        SetAllExpanded(false);
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Quick Tips", Styles.Bold);
                EditorGUILayout.LabelField("• Use stable Question IDs for Scenario routing (hidden by default).", Styles.Small);
                EditorGUILayout.LabelField("• Single Choice = one correct answer.", Styles.Small);
                EditorGUILayout.LabelField("• Multiple Choice can allow partial credit.", Styles.Small);
            }
        }

        float GetQuestionHeight(int index)
        {
            var q = _questionsProp.GetArrayElementAtIndex(index);
            if (q == null) return EditorGUIUtility.singleLineHeight * 2f;

            float h = 0f;
            h += EditorGUIUtility.singleLineHeight + 8f; // title row

            if (!IsExpanded(index))
                return h + 8f;

            if (_showIds) h += PH(q, "id");
            h += PH(q, "prompt");
            h += PH(q, "type");
            h += PH(q, "points");
            h += PH(q, "allowPartialCredit");

            var answers = q.FindPropertyRelative("answers");
            h += GetAnswersList(index, answers).GetHeight();
            h += 14f;
            return h;
        }

        void DrawQuestionElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var q = _questionsProp.GetArrayElementAtIndex(index);
            if (q == null) return;

            EnsureQuestionId(q);
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;

            var title = $"Q{index + 1}";
            var typeProp = q.FindPropertyRelative("type");
            var pointsProp = q.FindPropertyRelative("points");
            var idProp = q.FindPropertyRelative("id");

            var accent = new Color(0.35f, 0.65f, 1f);
            if (typeProp != null && typeProp.enumValueIndex == (int)QuizAsset.QuestionType.MultipleChoice)
                accent = new Color(0.85f, 0.55f, 0.25f);
            var headerRect = new Rect(rect.x + 4, rect.y, rect.width - 8, rect.height + 4);
            EditorGUI.DrawRect(headerRect, Styles.HeaderBg);
            EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 3, headerRect.height), accent);

            bool expanded = IsExpanded(index);
            var foldRect = new Rect(headerRect.x + 8, headerRect.y + 2, 16, rect.height);
            expanded = EditorGUI.Foldout(foldRect, expanded, GUIContent.none, true);
            _expanded[index] = expanded;

            var titleRect = new Rect(headerRect.x + 26, headerRect.y + 2, headerRect.width - 26, rect.height);
            EditorGUI.LabelField(titleRect, title, Styles.Bold);

            var badgeRect = new Rect(headerRect.x + headerRect.width - 200, headerRect.y + 2, 90, rect.height);
            if (typeProp != null)
            {
                var type = typeProp.enumDisplayNames[typeProp.enumValueIndex];
                Styles.DrawBadge(badgeRect, type, accent);
            }
            var ptsRect = new Rect(headerRect.x + headerRect.width - 100, headerRect.y + 2, 90, rect.height);
            if (pointsProp != null)
                Styles.DrawBadge(ptsRect, $"{pointsProp.floatValue:0.##} pts", new Color(0.35f, 0.38f, 0.45f));

            rect.y = headerRect.yMax + 6;

            if (!expanded)
                return;

            if (_showIds)
            {
                EditorGUI.BeginDisabledGroup(true);
                DrawProp(ref rect, q, "id", "Question ID");
                EditorGUI.EndDisabledGroup();

                var row = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                var copyRect = new Rect(row.x, row.y, 90, row.height);
                var regenRect = new Rect(copyRect.xMax + 6, row.y, 110, row.height);
                if (GUI.Button(copyRect, "Copy ID", Styles.Mid))
                    EditorGUIUtility.systemCopyBuffer = idProp?.stringValue ?? "";
                if (GUI.Button(regenRect, "Regenerate", Styles.Mid))
                    idProp.stringValue = Guid.NewGuid().ToString();
                rect.y += row.height + 4;
            }

            DrawProp(ref rect, q, "prompt", "Prompt");
            DrawProp(ref rect, q, "type", "Type");
            DrawProp(ref rect, q, "points", "Points");
            DrawProp(ref rect, q, "allowPartialCredit", "Allow Partial Credit");

            var answers = q.FindPropertyRelative("answers");
            var list = GetAnswersList(index, answers);
            var listRect = new Rect(rect.x, rect.y, rect.width, list.GetHeight());
            list.DoList(listRect);
        }

        ReorderableList GetAnswersList(int questionIndex, SerializedProperty answersProp)
        {
            if (_answersLists.TryGetValue(questionIndex, out var list)) return list;

            list = new ReorderableList(serializedObject, answersProp, true, true, true, true);
            list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Answers", Styles.Bold);
            list.elementHeightCallback = item =>
            {
                float h = 0f;
                h += EditorGUIUtility.singleLineHeight + 2f; // text
                h += EditorGUIUtility.singleLineHeight + 2f; // correct
                h += EditorGUIUtility.singleLineHeight + 2f; // explanation toggle
                if (IsExplanationVisible(questionIndex, item))
                    h += EditorGUIUtility.singleLineHeight * 2f + 6f; // explanation
                return h;
            };
            list.drawElementCallback = (r, i, a, f) =>
            {
                var el = answersProp.GetArrayElementAtIndex(i);
                if (el == null) return;

                r.y += 2;
                var text = el.FindPropertyRelative("text");
                var correct = el.FindPropertyRelative("isCorrect");
                var expl = el.FindPropertyRelative("explanation");

                var line = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, line), text, new GUIContent($"Answer {i + 1}"));
                r.y += line + 2;
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, line), correct, new GUIContent("Correct"));
                r.y += line + 2;
                bool show = IsExplanationVisible(questionIndex, i);
                var btnRect = new Rect(r.x, r.y, 140, line);
                if (GUI.Button(btnRect, show ? "Hide Explanation" : "Add Explanation", Styles.Mid))
                    SetExplanationVisible(questionIndex, i, !show);
                r.y += line + 2;

                if (show)
                {
                    var explH = line * 2f;
                    EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, explH), expl, new GUIContent("Explanation"), true);
                }
            };

            _answersLists[questionIndex] = list;
            return list;
        }

        void DrawValidation()
        {
            if (_questionsProp == null) return;

            var ids = new HashSet<string>();
            var dupes = new List<string>();
            int empty = 0;

            for (int i = 0; i < _questionsProp.arraySize; i++)
            {
                var q = _questionsProp.GetArrayElementAtIndex(i);
                var idProp = q?.FindPropertyRelative("id");
                string id = idProp != null ? idProp.stringValue : "";
                if (string.IsNullOrWhiteSpace(id)) { empty++; continue; }
                if (!ids.Add(id)) dupes.Add(id);

                var answers = q.FindPropertyRelative("answers");
                if (answers == null || answers.arraySize == 0)
                    EditorGUILayout.HelpBox($"Question {i + 1} has no answers.", MessageType.Warning);
                else
                {
                    int correct = 0;
                    for (int a = 0; a < answers.arraySize; a++)
                    {
                        var el = answers.GetArrayElementAtIndex(a);
                        var c = el?.FindPropertyRelative("isCorrect");
                        if (c != null && c.boolValue) correct++;
                    }
                    if (correct == 0)
                        EditorGUILayout.HelpBox($"Question {i + 1} has no correct answer marked.", MessageType.Warning);
                }
            }

            if (empty > 0)
                EditorGUILayout.HelpBox($"{empty} question(s) are missing IDs. IDs are used for stable routing.", MessageType.Info);

            if (dupes.Count > 0)
                EditorGUILayout.HelpBox("Duplicate Question IDs: " + string.Join(", ", dupes), MessageType.Error);
        }

        static void DrawProp(ref Rect rect, SerializedProperty parent, string name, string label)
        {
            var prop = parent.FindPropertyRelative(name);
            if (prop == null) return;
            var h = EditorGUI.GetPropertyHeight(prop, true);
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, h), prop, new GUIContent(label), true);
            rect.y += h + 2f;
        }

        static float PH(SerializedProperty parent, string name)
        {
            var prop = parent.FindPropertyRelative(name);
            if (prop == null) return EditorGUIUtility.singleLineHeight + 2f;
            return EditorGUI.GetPropertyHeight(prop, true) + 2f;
        }

        static bool IsExpanded(int index)
        {
            if (!_expanded.TryGetValue(index, out var v)) v = true;
            return v;
        }

        static void SetAllExpanded(bool expanded)
        {
            _expanded.Clear();
            for (int i = 0; i < 512; i++) _expanded[i] = expanded;
        }

        static void EnsureQuestionId(SerializedProperty q)
        {
            if (q == null) return;
            var idProp = q.FindPropertyRelative("id");
            if (idProp == null) return;
            if (string.IsNullOrWhiteSpace(idProp.stringValue))
                idProp.stringValue = Guid.NewGuid().ToString();
        }

        static void ResetQuestion(SerializedProperty q)
        {
            if (q == null) return;
            var idProp = q.FindPropertyRelative("id");
            var promptProp = q.FindPropertyRelative("prompt");
            var typeProp = q.FindPropertyRelative("type");
            var pointsProp = q.FindPropertyRelative("points");
            var partialProp = q.FindPropertyRelative("allowPartialCredit");
            var answersProp = q.FindPropertyRelative("answers");

            if (idProp != null) idProp.stringValue = Guid.NewGuid().ToString();
            if (promptProp != null) promptProp.stringValue = "";
            if (typeProp != null) typeProp.enumValueIndex = 0;
            if (pointsProp != null) pointsProp.floatValue = 1f;
            if (partialProp != null) partialProp.boolValue = false;
            if (answersProp != null && answersProp.isArray) answersProp.ClearArray();
        }

        static bool IsExplanationVisible(int qIndex, int aIndex)
        {
            return _showExplanation.TryGetValue($"{qIndex}:{aIndex}", out var v) && v;
        }

        static void SetExplanationVisible(int qIndex, int aIndex, bool on)
        {
            _showExplanation[$"{qIndex}:{aIndex}"] = on;
        }

        static class Styles
        {
            static bool _inited;
            public static GUIStyle SectionBox;
            public static readonly Color HeaderBg = new Color(0.11f, 0.12f, 0.15f);
            public static GUIStyle HeaderTitle;
            public static GUIStyle Bold;
            public static GUIStyle Small;
            public static GUIStyle Muted;
            public static GUIStyle Primary;
            public static GUIStyle Mid;
            public static GUIStyle OuterBox;
            public static GUIStyle InfoBox;
            public static readonly Color RowEven = new Color(0.16f, 0.18f, 0.22f);
            public static readonly Color RowOdd = new Color(0.14f, 0.16f, 0.19f);

            public static void Ensure()
            {
                if (_inited) return;
                if (EditorStyles.boldLabel == null) return;

                HeaderTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                Bold = new GUIStyle(EditorStyles.boldLabel);
                Small = new GUIStyle(EditorStyles.label) { fontSize = 10, wordWrap = true };
                Muted = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.8f, 0.82f, 0.86f, 0.8f) } };

                Primary = new GUIStyle(EditorStyles.miniButton);
                Mid = new GUIStyle(EditorStyles.miniButton);

                OuterBox = new GUIStyle("HelpBox") { padding = new RectOffset(8, 8, 6, 6) };
                InfoBox = new GUIStyle("HelpBox") { padding = new RectOffset(8, 8, 8, 8) };

                SectionBox = new GUIStyle("HelpBox")
                {
                    margin = EditorStyles.helpBox.margin,
                    padding = new RectOffset(8, 8, 8, 8)
                };

                _inited = true;
            }

            public static bool Section(string title, bool open, Action drawBody)
            {
                Ensure();
                using (new EditorGUILayout.VerticalScope(SectionBox))
                {
                    var header = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
                    EditorGUI.DrawRect(header, HeaderBg);

                    var foldRect = new Rect(header.x + 15, header.y + 3, header.width - 12, header.height - 6);
                    open = EditorGUI.Foldout(foldRect, open, title, true, HeaderTitle);

                    if (open)
                    {
                        EditorGUILayout.Space(2);
                        drawBody?.Invoke();
                    }
                }
                return open;
            }

            public static void DrawBadge(Rect r, string text, Color color)
            {
                var bg = new Rect(r.x, r.y + 2, r.width, r.height - 4);
                EditorGUI.DrawRect(bg, color);
                var txt = new Rect(r.x + 6, r.y, r.width - 12, r.height);
                EditorGUI.LabelField(txt, text, new GUIStyle(EditorStyles.whiteBoldLabel) { alignment = TextAnchor.MiddleLeft });
            }
        }
    }
}
#endif
