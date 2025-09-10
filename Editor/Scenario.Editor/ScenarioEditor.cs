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
        ReorderableList list;
        GUIStyle titleStyle;

        void OnEnable()
        {
            FindProps();
            BuildList();
        }

        void FindProps()
        {
            if (target == null) return;
            stepsProp = serializedObject.FindProperty("steps");
            titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        }

        void BuildList()
        {
            if (stepsProp == null) return;

            list = new ReorderableList(serializedObject, stepsProp, true, true, true, true);

            list.drawHeaderCallback = r =>
                EditorGUI.LabelField(r, "Steps (Timeline → Cue Cards → Question → …)");

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
                return inner + EditorGUIUtility.singleLineHeight + 8;
            };

            list.drawElementCallback = (rect, index, active, focused) =>
            {
                var el = stepsProp.GetArrayElementAtIndex(index);

                if (el == null || el.managedReferenceValue == null)
                {
                    var header = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
                    EditorGUI.LabelField(header, $"{index:00}. <missing step>", titleStyle);
                    var fix = new Rect(rect.x, header.y + header.height + 4, rect.width, EditorGUIUtility.singleLineHeight);
                    if (GUI.Button(fix, "Remove null entry"))
                    {
                        stepsProp.DeleteArrayElementAtIndex(index);
                        serializedObject.ApplyModifiedProperties();
                    }
                    return;
                }

                string full = el.managedReferenceFullTypename ?? "";
                string label =
                    full.Contains(nameof(Runtime.TimelineStep)) ? $"{index:00}. Timeline" :
                    full.Contains(nameof(Runtime.CueCardsStep)) ? $"{index:00}. Cue Cards" :
                    full.Contains(nameof(Runtime.QuestionStep)) ? $"{index:00}. Question" :
                    $"{index:00}. Step";

                var header2 = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(header2, label, titleStyle);

                var body = new Rect(
                    rect.x, header2.y + header2.height + 4,
                    rect.width, EditorGUI.GetPropertyHeight(el, true));

                EditorGUI.PropertyField(body, el, GUIContent.none, true);
            };
        }

        void AddStep(Type t)
        {
            serializedObject.Update();
            int i = stepsProp.arraySize;
            stepsProp.InsertArrayElementAtIndex(i);
            stepsProp.GetArrayElementAtIndex(i).managedReferenceValue = Activator.CreateInstance(t);
            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            if (target == null) return;

            serializedObject.UpdateIfRequiredOrScript();
            if (stepsProp == null) { FindProps(); BuildList(); }

            var sc = target as Runtime.Scenario;
            if (sc != null && sc.steps != null)
            {
                for (int i = sc.steps.Count - 1; i >= 0; i--)
                    if (sc.steps[i] == null)
                    {
                        sc.steps.RemoveAt(i);
                        EditorUtility.SetDirty(sc);
                    }
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox(
                "Authoring:\n" +
                "• Timeline: assign the scene PlayableDirector\n" +
                "• Cue Cards: add cards and Cue Times (sec, max per card). Leave empty for tap-only\n" +
                "• Question: assign Panel root, Animator and Buttons then add StatEffects on each button",
                MessageType.Info);

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(150)))
            {
                GUILayout.Space(2);
                if (GUILayout.Button("Open Scenario Graph", GUILayout.Height(24)))
                    ScenarioGraphWindow.Open(sc);
            }
            EditorGUILayout.EndHorizontal();

            if (list == null) BuildList();
            if (list != null) list.DoLayoutList();

            serializedObject.ApplyModifiedProperties();

            DrawRouting(sc);
            DrawValidation(sc);
        }

        void DrawRouting(Runtime.Scenario sc)
        {
            if (!sc || sc.steps == null || sc.steps.Count == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Routing (quick links)", EditorStyles.boldLabel);

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

                if (s is Runtime.TimelineStep tl)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{i:00} Timeline", GUILayout.Width(120));
                    int choice = Popup(tl.nextGuid);
                    string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                    if (newGuid != tl.nextGuid) { Undo.RecordObject(sc, "Route Change"); tl.nextGuid = newGuid; EditorUtility.SetDirty(sc); }
                    EditorGUILayout.EndHorizontal();
                }
                else if (s is Runtime.CueCardsStep cc)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{i:00} Cue Cards", GUILayout.Width(120));
                    int choice = Popup(cc.nextGuid);
                    string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                    if (newGuid != cc.nextGuid) { Undo.RecordObject(sc, "Route Change"); cc.nextGuid = newGuid; EditorUtility.SetDirty(sc); }
                    EditorGUILayout.EndHorizontal();
                }
                else if (s is Runtime.QuestionStep q)
                {
                    EditorGUILayout.LabelField($"{i:00} Question", EditorStyles.boldLabel);
                    if (q.choices == null || q.choices.Count == 0) { EditorGUILayout.LabelField("  (no choices)"); continue; }

                    for (int c = 0; c < q.choices.Count; c++)
                    {
                        var ch = q.choices[c];
                        if (ch == null) continue;

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"  Choice {c}", GUILayout.Width(120));
                        int choice = Popup(ch.nextGuid);
                        string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                        if (newGuid != ch.nextGuid) { Undo.RecordObject(sc, "Route Change"); ch.nextGuid = newGuid; EditorUtility.SetDirty(sc); }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }

        void DrawValidation(Runtime.Scenario sc)
        {
            if (sc == null || sc.steps == null) return;
            EditorGUILayout.Space();

            for (int i = 0; i < sc.steps.Count; i++)
            {
                var s = sc.steps[i];
                if (s == null)
                {
                    EditorGUILayout.HelpBox($"Step {i}: is null (remove it).", MessageType.Warning);
                    continue;
                }

                if (s is Runtime.TimelineStep tl)
                {
                    if (!tl.director)
                        EditorGUILayout.HelpBox($"Step {i}: Timeline has no Director.", MessageType.Warning);
                }
                else if (s is Runtime.CueCardsStep cc)
                {
                    if (cc.cards == null || cc.cards.Length == 0)
                        EditorGUILayout.HelpBox($"Step {i}: Cue Cards has no cards.", MessageType.Warning);

                    if (cc.cueTimes != null && cc.cueTimes.Length > 1 &&
                        (cc.cards == null || cc.cueTimes.Length != cc.cards.Length))
                        EditorGUILayout.HelpBox($"Step {i}: Cue Times should be 1 or match cards count. Extra cards use the last time.", MessageType.Info);
                }
                else if (s is Runtime.QuestionStep q)
                {
                    if (!q.panelRoot)
                        EditorGUILayout.HelpBox($"Step {i}: Question has no Panel Root.", MessageType.Warning);

                    if (q.choices == null || q.choices.Count == 0)
                        EditorGUILayout.HelpBox($"Step {i}: Question has no choices.", MessageType.Warning);
                    else
                        for (int c = 0; c < q.choices.Count; c++)
                            if (q.choices[c] != null && !q.choices[c].button)
                                EditorGUILayout.HelpBox($"Step {i} Choice {c}: Button not set.", MessageType.Info);
                }
            }
        }
    }

    // -------- Custom drawers (hide guid/graphPos/nextGuid) --------

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
