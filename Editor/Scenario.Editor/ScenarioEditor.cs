#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Pitech.XR.Scenario.Editor
{
    using Runtime = Pitech.XR.Scenario;

    [CustomEditor(typeof(Runtime.Scenario))]
    public class ScenarioEditor : UnityEditor.Editor
    {
        SerializedProperty stepsProp;
        SerializedProperty titleProp;
        ReorderableList list;

        // foldout prefs (persist per user)
        const string FoldStepsKey = "pitech.xr.scenario.fold.steps";
        const string FoldRoutingKey = "pitech.xr.scenario.fold.routing";
        const string FoldValidationKey = "pitech.xr.scenario.fold.validation";

        bool foldSteps;
        bool foldRouting;
        bool foldValidation;

        void OnEnable()
        {
            FindProps();
            BuildList();

            foldSteps = EditorPrefs.GetBool(FoldStepsKey, true);
            foldRouting = EditorPrefs.GetBool(FoldRoutingKey, true);
            foldValidation = EditorPrefs.GetBool(FoldValidationKey, true);
        }

        void OnDisable()
        {
            EditorPrefs.SetBool(FoldStepsKey, foldSteps);
            EditorPrefs.SetBool(FoldRoutingKey, foldRouting);
            EditorPrefs.SetBool(FoldValidationKey, foldValidation);
        }

        void FindProps()
        {
            if (target == null) return;
            stepsProp = serializedObject.FindProperty("steps");
            titleProp = serializedObject.FindProperty("title");
        }

        public override void OnInspectorGUI()
        {
            if (target == null) return;

            serializedObject.UpdateIfRequiredOrScript();
            if (stepsProp == null) FindProps();
            if (list == null) BuildList();

            // TOP BAR
            DrawTopBar();

            // AUTHORING HINT
            using (new EditorGUILayout.VerticalScope(Styles.InfoBox))
            {
                EditorGUILayout.LabelField("Authoring", Styles.Bold);
                EditorGUILayout.LabelField("• Timeline: assign the scene PlayableDirector", Styles.Small);
                EditorGUILayout.LabelField("• Cue Cards: add cards and Cue Times (sec). Empty = tap-only", Styles.Small);
                EditorGUILayout.LabelField("• Question: set Panel Root, Animator and Buttons then add Effects", Styles.Small);
                EditorGUILayout.LabelField("• Selection: set SelectionLists, choose list (Key or Index), rule & completion.", Styles.Small);
                EditorGUILayout.LabelField("• Insert: set item, target trigger and optional attach behaviour.", Styles.Small);
            }

            // STEPS SECTION
            foldSteps = Styles.Section("Steps", foldSteps, () =>
            {
                if (list != null) list.DoLayoutList();
            });

            // ROUTING SECTION
            var sc = target as Runtime.Scenario;
            foldRouting = Styles.Section("Routing (quick links)", foldRouting, () =>
            {
                DrawRouting(sc);
            });

            // VALIDATION
            foldValidation = Styles.Section("Validation", foldValidation, () =>
            {
                DrawValidation(sc);
            });

            serializedObject.ApplyModifiedProperties();
        }

        void DrawTopBar()
        {
            var sc = target as Runtime.Scenario;

            using (new EditorGUILayout.VerticalScope(Styles.InfoBox))
            {
                // Title
                EditorGUILayout.LabelField("Scenario Title", Styles.HeaderTitle);

                if (titleProp != null)
                {
                    EditorGUI.BeginChangeCheck();
                    string newTitle = EditorGUILayout.TextField(GUIContent.none, titleProp.stringValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        titleProp.stringValue = newTitle;
                        serializedObject.ApplyModifiedProperties();

                        if (sc && !string.IsNullOrEmpty(newTitle) && sc.gameObject.name == "Scenario")
                        {
                            sc.gameObject.name = newTitle;
                            EditorUtility.SetDirty(sc);
                        }
                    }
                }

                // Big button
                EditorGUILayout.Space(4);
                var r = GUILayoutUtility.GetRect(
                    GUIContent.none, Styles.BigButton, GUILayout.Height(34), GUILayout.ExpandWidth(true)
                );

                var prevBg = GUI.backgroundColor;
                var prevCt = GUI.contentColor;
                GUI.backgroundColor = new Color(0.55f, 0.72f, 1.00f);
                GUI.contentColor = Color.white;

                if (GUI.Button(r, "★  Open Scenario Graph", Styles.BigButton))
                    ScenarioGraphWindow.Open(sc);

                GUI.contentColor = prevCt;
                GUI.backgroundColor = prevBg;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Ping", Styles.Mid, GUILayout.Height(22)))
                        EditorGUIUtility.PingObject(sc);

                    if (GUILayout.Button("Clear Nulls", Styles.Mid, GUILayout.Height(22)))
                    {
                        if (sc?.steps != null)
                        {
                            for (int i = sc.steps.Count - 1; i >= 0; i--)
                                if (sc.steps[i] == null) sc.steps.RemoveAt(i);
                            EditorUtility.SetDirty(sc);
                        }
                    }
                }
            }
        }

        // ================== Reorderable List ==================

        void BuildList()
        {
            if (stepsProp == null) return;

            list = new ReorderableList(serializedObject, stepsProp, true, true, true, true);

            list.drawHeaderCallback = r =>
            {
                r.x += Styles.SectionBox.padding.left;
                r.width -= Styles.SectionBox.padding.horizontal;
                EditorGUI.LabelField(r, "Create Steps (Example: Timeline → Cue Cards → Question → …)", Styles.Bold);
            };

            list.onAddDropdownCallback = (rect, _) =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Add Timeline"), false, () => AddStep(typeof(Runtime.TimelineStep)));
                menu.AddItem(new GUIContent("Add Cue Cards"), false, () => AddStep(typeof(Runtime.CueCardsStep)));
                menu.AddItem(new GUIContent("Add Question"), false, () => AddStep(typeof(Runtime.QuestionStep)));
                menu.AddItem(new GUIContent("Add Selection"), false, () => AddStep(typeof(Runtime.SelectionStep)));
                menu.AddItem(new GUIContent("Add Insert"), false, () => AddStep(typeof(Runtime.InsertStep)));
                menu.AddItem(new GUIContent("Add Event"), false, () => AddStep(typeof(Runtime.EventStep)));
                menu.ShowAsContext();
            };

            list.elementHeightCallback = i =>
            {
                var p = stepsProp.GetArrayElementAtIndex(i);
                if (p == null || p.managedReferenceValue == null)
                    return EditorGUIUtility.singleLineHeight * 2 + 12;

                float inner = EditorGUI.GetPropertyHeight(p, true);
                return inner + EditorGUIUtility.singleLineHeight + 10;
            };

            list.drawElementBackgroundCallback = (rect, index, active, focused) =>
            {
                if (Event.current.type == EventType.Repaint)
                {
                    var c = (index % 2 == 0) ? Styles.RowEven : Styles.RowOdd;
                    EditorGUI.DrawRect(rect, c);
                }
            };

            list.drawElementCallback = (rect, index, active, focused) =>
            {
                var el = stepsProp.GetArrayElementAtIndex(index);

                if (el == null || el.managedReferenceValue == null)
                {
                    var header = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, EditorGUIUtility.singleLineHeight);
                    EditorGUI.LabelField(header, $"{index:00}. <missing step>", Styles.Muted);
                    var fix = new Rect(rect.x + 4, header.y + header.height + 3, rect.width - 8, EditorGUIUtility.singleLineHeight);
                    if (GUI.Button(fix, "Remove null entry", Styles.Mid))
                    {
                        stepsProp.DeleteArrayElementAtIndex(index);
                        serializedObject.ApplyModifiedProperties();
                    }
                    return;
                }

                string full = el.managedReferenceFullTypename ?? "";
                string kind =
                    full.Contains(nameof(Runtime.TimelineStep)) ? "Timeline" :
                    full.Contains(nameof(Runtime.CueCardsStep)) ? "Cue Cards" :
                    full.Contains(nameof(Runtime.QuestionStep)) ? "Question" :
                    full.Contains(nameof(Runtime.SelectionStep)) ? "Selection" :
                    full.Contains(nameof(Runtime.InsertStep)) ? "Insert" :
                    full.Contains(nameof(Runtime.EventStep)) ? "Event" :
                    "Step";


                var header2 = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, EditorGUIUtility.singleLineHeight);
                DrawStepHeader(header2, index, kind);

                var body = new Rect(
                    rect.x + 4, header2.y + header2.height + 3,
                    rect.width - 8, EditorGUI.GetPropertyHeight(el, true));

                EditorGUI.PropertyField(body, el, GUIContent.none, true);

                var xRect = new Rect(rect.xMax - 22, rect.y + 2, 18, EditorGUIUtility.singleLineHeight - 2);
                if (GUI.Button(xRect, "✕", EditorStyles.miniButton))
                    RemoveStepAt(index);
            };

            list.onCanRemoveCallback = l => l.count > 0;

            list.onRemoveCallback = l =>
            {
                RemoveStepAt(l.index);
            };
        }

        void DrawStepHeader(Rect r, int index, string kind)
        {
            var left = new Rect(r.x, r.y, 50, r.height);
            EditorGUI.LabelField(left, $"{index:00}", Styles.Index);

            var badge = new Rect(left.xMax + 4, r.y + 1, 82, r.height - 2);
            Styles.DrawBadge(badge, kind);
        }

        void AddStep(Type t)
        {
            serializedObject.Update();
            int i = stepsProp.arraySize;
            stepsProp.InsertArrayElementAtIndex(i);
            var el = stepsProp.GetArrayElementAtIndex(i);
            el.managedReferenceValue = Activator.CreateInstance(t);
            serializedObject.ApplyModifiedProperties();
        }

        void RemoveStepAt(int index)
        {
            if (stepsProp == null) return;
            if (index < 0 || index >= stepsProp.arraySize) return;

            Undo.RecordObject(target, "Remove Step");

            stepsProp.DeleteArrayElementAtIndex(index);

            if (index < stepsProp.arraySize)
            {
                var el = stepsProp.GetArrayElementAtIndex(index);
                bool isManaged = el != null &&
                                 el.propertyType == SerializedPropertyType.ManagedReference;
                if (isManaged && el.managedReferenceValue == null)
                    stepsProp.DeleteArrayElementAtIndex(index);
            }

            serializedObject.ApplyModifiedProperties();
            GUI.FocusControl(null);
        }

        // ================== Routing ==================

        void DrawRouting(Runtime.Scenario sc)
        {
            if (!sc || sc.steps == null || sc.steps.Count == 0)
            {
                EditorGUILayout.HelpBox("No steps yet.", MessageType.Info);
                return;
            }

            var names = new List<string> { "(next in list)" };
            var guids = new List<string> { "" };

            for (int i = 0; i < sc.steps.Count; i++)
            {
                var s = sc.steps[i];
                if (s == null) continue;
                if (string.IsNullOrEmpty(s.guid)) s.guid = Guid.NewGuid().ToString();
                names.Add($"{i:00} • {s.Kind}");
                guids.Add(s.guid);
            }

            int Popup(string currentGuid)
            {
                int idx = Mathf.Max(0, guids.IndexOf(currentGuid));
                return EditorGUILayout.Popup(idx, names.ToArray());
            }

            for (int i = 0; i < sc.steps.Count; i++)
            {
                var s = sc.steps[i];
                if (s == null) continue;

                using (new EditorGUILayout.VerticalScope(Styles.OuterBox))
                {
                    EditorGUILayout.LabelField($"{i:00}  {s.Kind}", Styles.Bold);

                    if (s is Runtime.TimelineStep tl)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Next", GUILayout.Width(60));
                            int choice = Popup(tl.nextGuid);
                            string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                            if (newGuid != tl.nextGuid)
                            {
                                Undo.RecordObject(sc, "Route Change");
                                tl.nextGuid = newGuid;
                                EditorUtility.SetDirty(sc);
                            }
                        }
                    }
                    else if (s is Runtime.CueCardsStep cc)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Next", GUILayout.Width(60));
                            int choice = Popup(cc.nextGuid);
                            string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                            if (newGuid != cc.nextGuid)
                            {
                                Undo.RecordObject(sc, "Route Change");
                                cc.nextGuid = newGuid;
                                EditorUtility.SetDirty(sc);
                            }
                        }
                    }
                    else if (s is Runtime.QuestionStep q)
                    {
                        if (q.choices == null || q.choices.Count == 0)
                        {
                            EditorGUILayout.LabelField("No choices", Styles.Muted);
                        }
                        else
                        {
                            for (int c = 0; c < q.choices.Count; c++)
                            {
                                var ch = q.choices[c];
                                if (ch == null) continue;

                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    GUILayout.Label($"Choice {c}", GUILayout.Width(80));
                                    int choice = Popup(ch.nextGuid);
                                    string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                                    if (newGuid != ch.nextGuid)
                                    {
                                        Undo.RecordObject(sc, "Route Change");
                                        ch.nextGuid = newGuid;
                                        EditorUtility.SetDirty(sc);
                                    }
                                }
                            }
                        }
                    }
                    else if (s is Runtime.SelectionStep sel)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Correct", GUILayout.Width(60));
                            int iSel = Popup(sel.correctNextGuid);
                            string newGuid = guids[Mathf.Clamp(iSel, 0, guids.Count - 1)];
                            if (newGuid != sel.correctNextGuid)
                            {
                                Undo.RecordObject(sc, "Route Change");
                                sel.correctNextGuid = newGuid;
                                EditorUtility.SetDirty(sc);
                            }
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Wrong", GUILayout.Width(60));
                            int iWrong = Popup(sel.wrongNextGuid);
                            string newGuid = guids[Mathf.Clamp(iWrong, 0, guids.Count - 1)];
                            if (newGuid != sel.wrongNextGuid)
                            {
                                Undo.RecordObject(sc, "Route Change");
                                sel.wrongNextGuid = newGuid;
                                EditorUtility.SetDirty(sc);
                            }
                        }
                    }
                    else if (s is Runtime.InsertStep ins)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Next", GUILayout.Width(60));
                            int choice = Popup(ins.nextGuid);
                            string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                            if (newGuid != ins.nextGuid)
                            {
                                Undo.RecordObject(sc, "Route Change");
                                ins.nextGuid = newGuid;
                                EditorUtility.SetDirty(sc);
                            }
                        }
                    }
                    else if (s is Runtime.EventStep ev)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Next", GUILayout.Width(60));
                            int choice = Popup(ev.nextGuid);
                            string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                            if (newGuid != ev.nextGuid)
                            {
                                Undo.RecordObject(sc, "Route Change");
                                ev.nextGuid = newGuid;
                                EditorUtility.SetDirty(sc);
                            }
                        }
                    }
                }
            }
        }

        // ================== Validation ==================

        void DrawValidation(Runtime.Scenario sc)
        {
            if (sc == null || sc.steps == null) return;
            int warnings = 0;

            for (int i = 0; i < sc.steps.Count; i++)
            {
                var s = sc.steps[i];
                if (s == null)
                {
                    Styles.Warn($"Step {i}: is null (remove it).");
                    warnings++;
                    continue;
                }

                if (s is Runtime.TimelineStep tl)
                {
                    if (!tl.director) { Styles.Warn($"Step {i}: Timeline has no Director."); warnings++; }
                }
                else if (s is Runtime.CueCardsStep cc)
                {
                    if (cc.cards == null || cc.cards.Length == 0)
                    { Styles.Warn($"Step {i}: Cue Cards has no cards."); warnings++; }

                    if (cc.cueTimes != null && cc.cueTimes.Length > 1 &&
                        (cc.cards == null || cc.cueTimes.Length != cc.cards.Length))
                        Styles.Info($"Step {i}: Cue Times 1 value (all cards) or match card count.");
                }
                else if (s is Runtime.QuestionStep q)
                {
                    if (!q.panelRoot)
                    { Styles.Warn($"Step {i}: Question has no Panel Root."); warnings++; }

                    if (q.choices == null || q.choices.Count == 0)
                    { Styles.Warn($"Step {i}: Question has no choices."); warnings++; }
                    else
                    {
                        for (int c = 0; c < q.choices.Count; c++)
                            if (q.choices[c] != null && !q.choices[c].button)
                            { Styles.Info($"Step {i} Choice {c}: Button not set."); }
                    }
                }
                else if (s is Runtime.SelectionStep sel)
                {
                    if (!sel.lists)
                    { Styles.Warn($"Step {i}: Selection has no SelectionLists reference."); warnings++; }

                    if (string.IsNullOrEmpty(sel.listKey) && sel.listIndex < 0)
                        Styles.Info($"Step {i}: Selection has neither List Key nor List Index set.");

                    if (sel.lists)
                    {
                        if (sel.listIndex >= sel.lists.Count)
                            Styles.Info($"Step {i}: List Index {sel.listIndex} is out of range (0..{sel.lists.Count - 1}).");

                        if (!string.IsNullOrEmpty(sel.listKey))
                        {
                            bool found = false;
                            for (int k = 0; k < sel.lists.Count; k++)
                                if (sel.lists.lists[k] != null && sel.lists.lists[k].name == sel.listKey) { found = true; break; }
                            if (!found)
                                Styles.Info($"Step {i}: List Key \"{sel.listKey}\" not found in SelectionLists.");
                        }
                    }

                    var comp = (Runtime.SelectionStep.CompleteMode)sel.completion;
                    if (comp == Runtime.SelectionStep.CompleteMode.OnSubmitButton && !sel.submitButton)
                        Styles.Info($"Step {i}: Selection is OnSubmitButton but Submit Button is not set.");

                    if (sel.requiredSelections <= 0)
                        Styles.Info($"Step {i}: Required Selections is 0 (step may pass immediately).");
                }
                else if (s is Runtime.InsertStep ins)
                {
                    if (!ins.item)
                    { Styles.Warn($"Step {i}: Insert has no Item assigned."); warnings++; }
                    if (!ins.targetTrigger)
                    { Styles.Warn($"Step {i}: Insert has no Target Trigger assigned."); warnings++; }
                    else if (!ins.targetTrigger.isTrigger)
                    {
                        Styles.Info($"Step {i}: Insert target collider is not marked as Trigger (recommended).");
                    }

                    if (ins.positionTolerance <= 0f)
                        Styles.Info($"Step {i}: Insert position tolerance is 0 or negative, step may never complete.");
                }
                else if (s is Runtime.EventStep ev)
                {
                    if (ev.onEnter == null || ev.onEnter.GetPersistentEventCount() == 0)
                        Styles.Info($"Step {i}: Event has no listeners. It will only wait {ev.waitSeconds} seconds then continue.");
                }
            }

            if (warnings == 0)
                EditorGUILayout.HelpBox("No blocking issues found.", MessageType.None);
        }

        // ================== Styles ==================

        static class Styles
        {
            static readonly Color cRowEven = new Color(0.16f, 0.18f, 0.22f);
            static readonly Color cRowOdd = new Color(0.14f, 0.16f, 0.19f);
            static readonly Color cBadgeTimeline = new Color(0.20f, 0.42f, 0.85f);
            static readonly Color cBadgeCards = new Color(0.32f, 0.62f, 0.32f);
            static readonly Color cBadgeQuestion = new Color(0.76f, 0.45f, 0.22f);
            static readonly Color cBadgeSelection = new Color(0.58f, 0.38f, 0.78f);
            static readonly Color cBadgeInsert = new Color(0.90f, 0.75f, 0.25f);
            static readonly Color cBadgeEvent = new Color(0.30f, 0.70f, 0.75f);

            public static readonly GUIStyle SectionBox;
            public static readonly Color HeaderBg = new Color(0.11f, 0.12f, 0.15f);
            public static readonly GUIStyle HeaderTitle;
            public static readonly GUIStyle Bold;
            public static readonly GUIStyle Small;
            public static readonly GUIStyle Muted;
            public static readonly GUIStyle Index;
            public static readonly GUIStyle Primary;
            public static readonly GUIStyle Mid;
            public static readonly GUIStyle OuterBox;
            public static readonly GUIStyle InfoBox;
            public static readonly Color RowEven = cRowEven;
            public static readonly Color RowOdd = cRowOdd;
            public static readonly GUIStyle BigButton;

            static Styles()
            {
                HeaderTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };

                Bold = new GUIStyle(EditorStyles.boldLabel);
                Small = new GUIStyle(EditorStyles.label) { fontSize = 10, wordWrap = true };
                Muted = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.8f, 0.82f, 0.86f, 0.8f) } };
                Index = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };

                Primary = new GUIStyle(EditorStyles.miniButton);
                Mid = new GUIStyle(EditorStyles.miniButton);

                OuterBox = new GUIStyle("HelpBox") { padding = new RectOffset(8, 8, 6, 6) };
                InfoBox = new GUIStyle("HelpBox") { padding = new RectOffset(8, 8, 8, 8) };

                BigButton = new GUIStyle(EditorStyles.miniButton)
                {
                    fixedHeight = 0,
                    stretchHeight = true,
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(18, 18, 9, 9),
                    margin = new RectOffset(8, 8, 4, 8),
                    wordWrap = false,
                    clipping = TextClipping.Overflow,
                    contentOffset = new Vector2(0, 1)
                };

                SectionBox = new GUIStyle("HelpBox")
                {
                    margin = EditorStyles.helpBox.margin,
                    padding = new RectOffset(8, 8, 8, 8)
                };
            }

            public static bool Section(string title, bool open, Action drawBody)
            {
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

            public static void DrawBadge(Rect r, string kind)
            {
                var col = cBadgeTimeline;
                if (kind == "Cue Cards") col = cBadgeCards;
                else if (kind == "Question") col = cBadgeQuestion;
                else if (kind == "Selection") col = cBadgeSelection;
                else if (kind == "Insert") col = cBadgeInsert;
                else if (kind == "Event") col = cBadgeEvent;

                var bg = new Rect(r.x, r.y, r.width, r.height);
                EditorGUI.DrawRect(bg, col);
                var txt = new Rect(r.x + 6, r.y, r.width - 12, r.height);
                EditorGUI.LabelField(txt, kind, new GUIStyle(EditorStyles.whiteBoldLabel) { alignment = TextAnchor.MiddleLeft });
            }

            public static void Warn(string msg) => EditorGUILayout.HelpBox(msg, MessageType.Warning);
            public static void Info(string msg) => EditorGUILayout.HelpBox(msg, MessageType.Info);
        }
    }

    // -------- Custom drawers --------

    [CustomPropertyDrawer(typeof(Runtime.TimelineStep))]
    class TimelineStepDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            if (p == null) return 0f;
            float h = 0;
            h += PH(p, "director");
            h += PH(p, "rewindOnEnter");
            h += PH(p, "waitForEnd");
            return h;
        }
        public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
        {
            if (p == null) return;
            Draw(ref r, p, "director", "Director");
            Draw(ref r, p, "rewindOnEnter", "Rewind On Enter");
            Draw(ref r, p, "waitForEnd", "Wait For End");
        }
        static float PH(SerializedProperty p, string name)
        {
            var sp = p.FindPropertyRelative(name);
            float baseH = EditorGUIUtility.singleLineHeight;
            return ((sp != null) ? EditorGUI.GetPropertyHeight(sp, true) : baseH)
                 + EditorGUIUtility.standardVerticalSpacing;
        }
        static void Draw(ref Rect r, SerializedProperty p, string name, string label)
        {
            var sp = p.FindPropertyRelative(name);
            if (sp == null) { r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; return; }
            var h = EditorGUI.GetPropertyHeight(sp, true);
            EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(label), true);
            r.y += h + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.CueCardsStep))]
    class CueCardsStepDrawer : PropertyDrawer
    {
        static readonly string[] fields =
        {
            "director","cards","cueTimes",
            "autoShowFirst","tapHint",
            "extraObject","extraShowAtIndex","hideExtraWithFinalTap","useRenderersForExtra",
            "fadeDuration","popScale","popDuration","fadeCurve","scaleCurve"
        };

        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            if (p == null) return 0f;
            float h = 0;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                if (sp == null) { h += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; continue; }
                h += EditorGUI.GetPropertyHeight(sp, true) + EditorGUIUtility.standardVerticalSpacing;
            }
            return h;
        }
        public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
        {
            if (p == null) return;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                var nicified = ObjectNames.NicifyVariableName(f);
                if (sp == null)
                {
                    EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), nicified);
                    r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    continue;
                }
                var h = EditorGUI.GetPropertyHeight(sp, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(nicified), true);
                r.y += h + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.QuestionStep))]
    class QuestionStepDrawer : PropertyDrawer
    {
        static readonly string[] fields = { "panelRoot", "panelAnimator", "showTrigger", "hideTrigger", "fallbackHideSeconds", "choices" };

        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            if (p == null) return 0f;
            float h = 0;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                if (sp == null) { h += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; continue; }
                h += EditorGUI.GetPropertyHeight(sp, true) + EditorGUIUtility.standardVerticalSpacing;
            }
            return h;
        }
        public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
        {
            if (p == null) return;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                var nicified = ObjectNames.NicifyVariableName(f);
                if (sp == null)
                {
                    EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), nicified);
                    r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    continue;
                }
                var h = EditorGUI.GetPropertyHeight(sp, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(nicified), true);
                r.y += h + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.Choice))]
    class ChoiceDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            if (p == null) return 0f;
            float h = 0;
            var btn = p.FindPropertyRelative("button");
            var fx = p.FindPropertyRelative("effects");
            if (btn != null) h += EditorGUI.GetPropertyHeight(btn, true) + EditorGUIUtility.standardVerticalSpacing;
            if (fx != null) h += EditorGUI.GetPropertyHeight(fx, true) + EditorGUIUtility.standardVerticalSpacing;
            return h;
        }
        public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
        {
            if (p == null) return;
            var btn = p.FindPropertyRelative("button");
            var fx = p.FindPropertyRelative("effects");

            if (btn != null)
            {
                var h0 = EditorGUI.GetPropertyHeight(btn, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h0), btn, new GUIContent("Button"), true);
                r.y += h0 + EditorGUIUtility.standardVerticalSpacing;
            }
            if (fx != null)
            {
                var h1 = EditorGUI.GetPropertyHeight(fx, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h1), fx, new GUIContent("Effects"), true);
            }
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.SelectionStep))]
    class SelectionStepDrawer : PropertyDrawer
    {
        static readonly string[] fields =
        {
            "lists",
            "listKey","listIndex",
            "resetOnEnter",
            "completion","submitButton",
            "requiredSelections","requireExactCount","allowedWrong","timeoutSeconds",
            "panelRoot","panelAnimator","showTrigger","hideTrigger","hint",
            "onCorrect","onWrong"
        };

        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            if (p == null) return 0f;
            float h = 0f;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                h += ((sp != null) ? EditorGUI.GetPropertyHeight(sp, true) : EditorGUIUtility.singleLineHeight)
                   + EditorGUIUtility.standardVerticalSpacing;
            }
            return h;
        }

        public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
        {
            if (p == null) return;

            var completionProp = p.FindPropertyRelative("completion");
            int completionMode = completionProp != null ? completionProp.enumValueIndex : 0;

            foreach (var f in fields)
            {
                if (f == "submitButton" && completionMode == 0)
                    continue;

                var sp = p.FindPropertyRelative(f);

                string label =
                    f == "listKey" ? "List Name" :
                    f == "listIndex" ? "(or) List Index" :
                    ObjectNames.NicifyVariableName(f);

                if (sp == null)
                {
                    EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), label);
                    r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    continue;
                }

                var h = EditorGUI.GetPropertyHeight(sp, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(label), true);
                r.y += h + EditorGUIUtility.standardVerticalSpacing;

                if (f == "completion")
                    completionMode = completionProp != null ? completionProp.enumValueIndex : 0;
            }
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.InsertStep))]
    class InsertStepDrawer : PropertyDrawer
    {
        static readonly string[] fields =
        {
            "item",
            "targetTrigger","attachTransform",
            "smoothAttach","parentToAttach","moveSpeed","rotateSpeed",
            "positionTolerance","angleTolerance"
        };


        public override float GetPropertyHeight(SerializedProperty p, GUIContent label)
        {
            if (p == null) return 0f;
            float h = 0f;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                h += ((sp != null) ? EditorGUI.GetPropertyHeight(sp, true) : EditorGUIUtility.singleLineHeight)
                   + EditorGUIUtility.standardVerticalSpacing;
            }
            return h;
        }

        public override void OnGUI(Rect r, SerializedProperty p, GUIContent label)
        {
            if (p == null) return;

            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                string nice =
                    f == "item" ? "Item" :
                    f == "targetTrigger" ? "Target Trigger" :
                    f == "attachTransform" ? "Attach Transform" :
                    f == "smoothAttach" ? "Smooth Attach" :
                    f == "parentToAttach" ? "Parent To Attach Point" :
                    ObjectNames.NicifyVariableName(f);


                if (sp == null)
                {
                    EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), nice);
                    r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    continue;
                }

                var h = EditorGUI.GetPropertyHeight(sp, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(nice), true);
                r.y += h + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }
    [CustomPropertyDrawer(typeof(Runtime.EventStep))]
    class EventStepDrawer : PropertyDrawer
    {
        static readonly string[] fields = { "onEnter", "waitSeconds" };

        public override float GetPropertyHeight(SerializedProperty p, GUIContent label)
        {
            if (p == null) return 0f;
            float h = 0f;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                h += ((sp != null) ? EditorGUI.GetPropertyHeight(sp, true) : EditorGUIUtility.singleLineHeight)
                   + EditorGUIUtility.standardVerticalSpacing;
            }
            return h;
        }

        public override void OnGUI(Rect r, SerializedProperty p, GUIContent label)
        {
            if (p == null) return;

            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                string nice =
                    f == "onEnter" ? "On Enter Events" :
                    f == "waitSeconds" ? "Wait Seconds Before Next" :
                    ObjectNames.NicifyVariableName(f);

                if (sp == null)
                {
                    EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), nice);
                    r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    continue;
                }

                var h = EditorGUI.GetPropertyHeight(sp, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(nice), true);
                r.y += h + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }

}
#endif
