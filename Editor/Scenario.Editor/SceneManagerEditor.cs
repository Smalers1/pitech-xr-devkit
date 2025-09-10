#if UNITY_EDITOR
using System.Reflection;
using Pitech.XR.Scenario;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

[CustomEditor(typeof(SceneManager))]
public class SceneManagerEditor : Editor
{
    SerializedProperty scenarioProp, statsConfigProp, statsUIProp, autoStartProp;
    GUIStyle title;

    void OnEnable()
    {
        scenarioProp = serializedObject.FindProperty("scenario");
        statsConfigProp = serializedObject.FindProperty("statsConfig");
        statsUIProp = serializedObject.FindProperty("statsUI");
        autoStartProp = serializedObject.FindProperty("autoStart");
        title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
    }

    public override void OnInspectorGUI()
    {
        var gm = (SceneManager)target;
        serializedObject.Update();

        EditorGUILayout.HelpBox("Flow: Timeline → (optional) Cue Cards → Question. Scenario holds all references.", MessageType.Info);

        EditorGUILayout.LabelField("References", title);
        EditorGUILayout.PropertyField(scenarioProp, new GUIContent("Scenario (in scene)"));
        EditorGUILayout.PropertyField(statsConfigProp, new GUIContent("Stats Config"));
        EditorGUILayout.PropertyField(statsUIProp, new GUIContent("Stats UI"));
        EditorGUILayout.PropertyField(autoStartProp, new GUIContent("Auto Start"));

        if (!scenarioProp.objectReferenceValue) EditorGUILayout.HelpBox("Assign a ScenarioComponent.", MessageType.Warning);
        if (!statsConfigProp.objectReferenceValue) EditorGUILayout.HelpBox("Assign a StatsConfig.", MessageType.Warning);

        EditorGUILayout.Space(6);
        DrawScenarioOverview(gm);
        EditorGUILayout.Space(6);
        DrawRuntimeControls(gm);

        serializedObject.ApplyModifiedProperties();
    }

    void DrawScenarioOverview(SceneManager gm)
    {
        var sc = gm.scenario;
        if (!sc) return;

        EditorGUILayout.LabelField("Scenario Overview", title);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (sc.steps == null || sc.steps.Count == 0)
            {
                EditorGUILayout.LabelField("No steps yet.");
                return;
            }

            for (int i = 0; i < sc.steps.Count; i++)
            {
                var s = sc.steps[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (s is TimelineStep tl)
                    {
                        var ok = tl.director ? "✓" : "✗";
                        EditorGUILayout.LabelField($"{i:00}. Timeline {ok}", GUILayout.Width(150));
                        EditorGUILayout.ObjectField(tl.director, typeof(PlayableDirector), true);
                        if (!tl.director) EditorGUILayout.HelpBox("Director not set", MessageType.Warning);
                    }
                    else if (s is CueCardsStep cc)
                    {
                        EditorGUILayout.LabelField($"{i:00}. Cue Cards", GUILayout.Width(150));
                        var times = cc.cueTimes != null ? cc.cueTimes.Length : 0;
                        EditorGUILayout.LabelField(times == 0 ? "tap-only" : $"{times} cue time(s)");
                    }
                    else if (s is QuestionStep q)
                    {
                        int btns = q.choices?.Count ?? 0;
                        EditorGUILayout.LabelField($"{i:00}. Question  • Buttons {btns}");
                    }
                }
            }
        }
    }

    void DrawRuntimeControls(SceneManager gm)
    {
        EditorGUILayout.LabelField("Runtime", title);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            var idxProp = gm.GetType().GetProperty("StepIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var idxText = idxProp != null ? (idxProp.GetValue(gm)?.ToString() ?? "n/a") : "n/a";
            EditorGUILayout.LabelField("Current Step", idxText);
            if (GUILayout.Button("Restart")) gm.Restart();
            if (!Application.isPlaying) EditorGUILayout.HelpBox("Enter Play mode to control.", MessageType.None);
        }
    }
}
#endif
