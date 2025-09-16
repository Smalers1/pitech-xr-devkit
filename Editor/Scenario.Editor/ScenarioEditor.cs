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
                EditorGUILayout.LabelField("• Cue Cards: add cards and Cue Times (sec). Empty = tap only", Styles.Small);
                EditorGUILayout.LabelField("• Question: set Panel Root, Animator and Buttons then add Effects", Styles.Small);
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

                // Full-width CTA, no clipping + light blue
                EditorGUILayout.Space(4);
                var r = GUILayoutUtility.GetRect(
                    GUIContent.none, Styles.BigButton, GUILayout.Height(34), GUILayout.ExpandWidth(true)
                );

                // Light blue + white text
                var prevBg = GUI.backgroundColor;
                var prevCt = GUI.contentColor;
                GUI.backgroundColor = new Color(0.55f, 0.72f, 1.00f); // lighter blue
                GUI.contentColor = Color.white;

                if (GUI.Button(r, "★  Open Scenario Graph", Styles.BigButton))
                    ScenarioGraphWindow.Open(sc);

                GUI.contentColor = prevCt;
                GUI.backgroundColor = prevBg;


                // Secondary actions on a separate right-aligned row
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

            // when you build the list
            list.drawHeaderCallback = r =>
            {
                // add a little inset so it lines up with the list body
                var rr = new Rect(r.x + 6, r.y, r.width - 12, r.height);
                EditorGUI.LabelField(rr, "Steps (Timeline → Cue Cards → Question → …)", Styles.Bold);
            };


            list.onAddDropdownCallback = (rect, _) =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Add Timeline"), false, () => AddStep(typeof(Runtime.TimelineStep)));
                menu.AddItem(new GUIContent("Add Cue Cards"), false, () => AddStep(typeof(Runtime.CueCardsStep)));
                menu.AddItem(new GUIContent("Add Question"), false, () => AddStep(typeof(Runtime.QuestionStep)));
                menu.ShowAsContext();
            };

            list.elementHeightCallback = i =>
            {
                var p = stepsProp.GetArrayElementAtIndex(i);
                if (p == null || p.managedReferenceValue == null)
                    return EditorGUIUtility.singleLineHeight * 2 + 12;

                float inner = EditorGUI.GetPropertyHeight(p, true);
                // extra for header
                return inner + EditorGUIUtility.singleLineHeight + 10;
            };

            list.drawElementBackgroundCallback = (rect, index, active, focused) =>
            {
                // zebra rows
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
                    "Step";

                // Header line with badge
                var header2 = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, EditorGUIUtility.singleLineHeight);
                DrawStepHeader(header2, index, kind);

                // Body
                var body = new Rect(
                    rect.x + 4, header2.y + header2.height + 3,
                    rect.width - 8, EditorGUI.GetPropertyHeight(el, true));

                EditorGUI.PropertyField(body, el, GUIContent.none, true);
            };
        }

        void DrawStepHeader(Rect r, int index, string kind)
        {
            // left: index
            var left = new Rect(r.x, r.y, 50, r.height);
            EditorGUI.LabelField(left, $"{index:00}", Styles.Index);

            // badge
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

        // ================== Routing ==================

        void DrawRouting(Runtime.Scenario sc)
        {
            if (!sc || sc.steps == null || sc.steps.Count == 0)
            {
                EditorGUILayout.HelpBox("No steps yet.", MessageType.Info);
                return;
            }

            // build names/guids
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

            // per step rows, boxed
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
            }

            if (warnings == 0)
                EditorGUILayout.HelpBox("No blocking issues found.", MessageType.None);
        }

        // ================== Styles ==================

        static class Styles
        {
            // palette (dark UI)
            static readonly Color cHeader = new Color(0.12f, 0.14f, 0.17f);
            static readonly Color cRowEven = new Color(0.16f, 0.18f, 0.22f);
            static readonly Color cRowOdd = new Color(0.14f, 0.16f, 0.19f);
            static readonly Color cBadgeTimeline = new Color(0.20f, 0.42f, 0.85f);
            static readonly Color cBadgeCards = new Color(0.32f, 0.62f, 0.32f);
            static readonly Color cBadgeQuestion = new Color(0.76f, 0.45f, 0.22f);

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

                OuterBox = new GUIStyle("HelpBox")
                {
                    padding = new RectOffset(8, 8, 6, 6)
                };

                InfoBox = new GUIStyle("HelpBox")
                {
                    padding = new RectOffset(8, 8, 8, 8)
                };
                BigButton = new GUIStyle(EditorStyles.miniButton)
                {
                    // let the layout height we request be used
                    fixedHeight = 0,
                    stretchHeight = true,

                    // make it feel like a “primary” button
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,

                    // extra vertical padding so text never clips
                    padding = new RectOffset(18, 18, 9, 9),

                    // a bit more outer space so it doesn’t look squeezed
                    margin = new RectOffset(8, 8, 4, 8),

                    // keep the label visible and centered
                    wordWrap = false,
                    clipping = TextClipping.Overflow,

                    // nudge text down one pixel (prevents top-edge cut on some DPIs)
                    contentOffset = new Vector2(0, 1)
                };

            }

            public static void DrawHeaderBackground(Rect r)
            {
                EditorGUI.DrawRect(r, cHeader);
                var bottom = new Rect(r.x, r.yMax - 1, r.width, 1);
                EditorGUI.DrawRect(bottom, new Color(0, 0, 0, 0.35f));
            }

            public static bool Section(string title, bool open, Action drawBody)
            {
                var rect = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(rect, new Color(0.11f, 0.12f, 0.15f));

                var foldRect = new Rect(rect.x + 6, rect.y + 4, rect.width - 12, rect.height - 8);
                open = EditorGUI.Foldout(foldRect, open, title, true, HeaderTitle);

                if (open)
                {
                    using (new EditorGUILayout.VerticalScope(OuterBox))
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

                var bg = new Rect(r.x, r.y, r.width, r.height);
                EditorGUI.DrawRect(bg, col);
                var txt = new Rect(r.x + 6, r.y, r.width - 12, r.height);
                EditorGUI.LabelField(txt, kind, new GUIStyle(EditorStyles.whiteBoldLabel) { alignment = TextAnchor.MiddleLeft });
            }

            public static void Warn(string msg) => EditorGUILayout.HelpBox(msg, MessageType.Warning);
            public static void Info(string msg) => EditorGUILayout.HelpBox(msg, MessageType.Info);
        }
    }

    // -------- Custom drawers (unchanged, just organized) --------

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
}
#endif
