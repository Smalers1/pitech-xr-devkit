#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using USceneManager = UnityEngine.SceneManagement.SceneManager; // avoid name clash

namespace Pitech.XR.Scenario.Editor
{
    [CustomEditor(typeof(Pitech.XR.Scenario.SceneManager), true)]
    public class SceneManagerEditor : UnityEditor.Editor
    {
        // Bind to your runtime fields here (adjust names if needed)
        SerializedProperty _scenarioProp;      // Pitech.XR.Scenario.Scenario
        SerializedProperty _statsConfigProp;   // Pitech.XR.Stats.StatsConfig
        SerializedProperty _statsUIProp;       // Pitech.XR.Stats.StatsUIController
        SerializedProperty _autoStartProp;     // bool

        const string ManagersRootName = "--- SCENE MANAGERS ---";

        // ❌ DO NOT cache EditorStyles in static fields — causes NREs on domain reload
        static GUIStyle TitleStyle => EditorStyles.boldLabel;

        void OnEnable()
        {
            // NOTE: change these strings if your runtime fields are named differently
            _scenarioProp = serializedObject.FindProperty("scenario");
            _statsConfigProp = serializedObject.FindProperty("statsConfig");
            _statsUIProp = serializedObject.FindProperty("statsUI");
            _autoStartProp = serializedObject.FindProperty("autoStart");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var gm = (Pitech.XR.Scenario.SceneManager)target;

            DrawHeaderHelp();
            EditorGUILayout.Space(6);

            // ============ FEATURES ============
            EditorGUILayout.LabelField("Features", TitleStyle);
            EditorGUILayout.Space(2);

            DrawScenarioFeature();
            EditorGUILayout.Space(6);
            DrawStatsFeature();

            EditorGUILayout.Space(8);
            if (_autoStartProp != null)
                EditorGUILayout.PropertyField(_autoStartProp, new GUIContent("Auto Start"));

            // ============ OVERVIEW & RUNTIME ============
            EditorGUILayout.Space(10);
            DrawScenarioOverview(gm);
            EditorGUILayout.Space(8);
            DrawRuntimeControls(gm);

            EditorGUILayout.Space(8);

            serializedObject.ApplyModifiedProperties();
        }

        // --------------------------------------------------------------------
        // Header
        // --------------------------------------------------------------------
        void DrawHeaderHelp()
        {
            EditorGUILayout.HelpBox(
                "Add only what your scene needs. Features are optional; the manager works fine with none.",
                MessageType.Info);
        }

        // --------------------------------------------------------------------
        // Features: Scenario
        // --------------------------------------------------------------------
        void DrawScenarioFeature()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scenario", TitleStyle);

                bool hasScenario = _scenarioProp != null && _scenarioProp.objectReferenceValue != null;

                if (hasScenario)
                {
                    // Exactly like the Stats "UI Controller" row: mini caption + object field + Ping/Clear
                    MiniCaption("Scenario");
                    ObjectFieldWithPingClear(serializedObject, _scenarioProp, "Scenario", "Pitech.XR.Scenario");
                }
                else
                {
                    EditorGUILayout.LabelField("Not added", EditorStyles.miniLabel);
                    if (GUILayout.Button("Add Scenario (create & assign)", GUILayout.Height(22)))
                        CreateAndAssignScenario();
                }
            }
        }




        // --------------------------------------------------------------------
        // Features: Stats (big buttons, like Scenario)
        // --------------------------------------------------------------------
        void DrawStatsFeature()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Stats", TitleStyle);

                bool hasUI = _statsUIProp != null && _statsUIProp.objectReferenceValue != null;
                bool hasConfig = _statsConfigProp != null && _statsConfigProp.objectReferenceValue != null;

                if (!hasUI && !hasConfig)
                    EditorGUILayout.LabelField("Not added", EditorStyles.miniLabel);

                // UI Controller row
                if (hasUI)
                {
                    MiniCaption("UI Controller");
                    ObjectFieldWithPingClear(serializedObject, _statsUIProp, "StatsUIController", "Pitech.XR.Stats");
                }
                else
                {
                    if (GUILayout.Button("Create StatsUIController", GUILayout.Height(22)))
                        CreateAndAssignStatsUI();
                }

                // Config row
                if (hasConfig)
                {
                    MiniCaption("Config");
                    ObjectFieldWithPingClear(serializedObject, _statsConfigProp, "StatsConfig", "Pitech.XR.Stats");
                }
                else
                {
                    if (GUILayout.Button("Create StatsConfig asset", GUILayout.Height(22)))
                        CreateAndAssignStatsConfig();
                }
            }
        }



        // --------------------------------------------------------------------
        // Overview (restored)
        // --------------------------------------------------------------------
        void DrawScenarioOverview(Pitech.XR.Scenario.SceneManager gm)
        {
            var scField = gm.GetType().GetField("scenario",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var sc = scField != null ? scField.GetValue(gm) as Pitech.XR.Scenario.Scenario : null;
            if (!sc) return;

            EditorGUILayout.LabelField("Scenario Overview", TitleStyle);
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
                        if (s is Pitech.XR.Scenario.TimelineStep tl)
                        {
                            var ok = tl.director ? "✓" : "✗";
                            EditorGUILayout.LabelField($"{i:00}. Timeline {ok}", GUILayout.Width(150));
                            EditorGUILayout.ObjectField(tl.director, typeof(PlayableDirector), true);
                            if (!tl.director) EditorGUILayout.HelpBox("Director not set", MessageType.Warning);
                        }
                        else if (s is Pitech.XR.Scenario.CueCardsStep cc)
                        {
                            EditorGUILayout.LabelField($"{i:00}. Cue Cards", GUILayout.Width(150));
                            var times = cc.cueTimes != null ? cc.cueTimes.Length : 0;
                            EditorGUILayout.LabelField(times == 0 ? "tap-only" : $"{times} cue time(s)");
                        }
                        else if (s is Pitech.XR.Scenario.QuestionStep q)
                        {
                            int btns = q.choices?.Count ?? 0;
                            EditorGUILayout.LabelField($"{i:00}. Question  • Buttons {btns}");
                        }
                    }
                }
            }
        }

        // --------------------------------------------------------------------
        // Runtime (restored)
        // --------------------------------------------------------------------
        void DrawRuntimeControls(Pitech.XR.Scenario.SceneManager gm)
        {
            EditorGUILayout.LabelField("Runtime", TitleStyle);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                var idxProp = gm.GetType().GetProperty("StepIndex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var idxText = idxProp != null ? (idxProp.GetValue(gm)?.ToString() ?? "n/a") : "n/a";
                EditorGUILayout.LabelField("Current Step", idxText);

                var restart = gm.GetType().GetMethod("Restart",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (GUILayout.Button("Restart") && restart != null)
                    restart.Invoke(gm, null);

                if (!Application.isPlaying)
                    EditorGUILayout.HelpBox("Enter Play mode to control.", MessageType.None);
            }
        }

        // --------------------------------------------------------------------
        // Creation helpers (Undo-friendly + robust property set)
        // --------------------------------------------------------------------
        static Transform EnsureManagersRoot()
        {
            var s = USceneManager.GetActiveScene();
            if (!s.IsValid() || !s.isLoaded)
            {
                EditorUtility.DisplayDialog("No Scene", "Open a scene first.", "OK");
                return null;
            }

            var root = s.GetRootGameObjects().FirstOrDefault(g => g.name == ManagersRootName);
            if (!root)
            {
                root = new GameObject(ManagersRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Managers Root");
                EditorSceneManager.MarkSceneDirty(s);
            }
            return root.transform;
        }

        static Type FindType(string simpleName, string @namespace = null)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == simpleName && (@namespace == null || t.Namespace == @namespace));
        }

        void CreateAndAssignScenario()
        {
            var parent = EnsureManagersRoot();
            if (!parent) return;

            var scenarioType = FindType("Scenario", "Pitech.XR.Scenario");
            if (scenarioType == null)
            {
                EditorUtility.DisplayDialog("Scenario not found",
                    "Could not find Pitech.XR.Scenario.Scenario.", "OK");
                return;
            }

            var go = new GameObject("Scenario");
            Undo.RegisterCreatedObjectUndo(go, "Create Scenario");
            var comp = go.AddComponent(scenarioType) as Component;
            go.transform.SetParent(parent, false);

            var so = new SerializedObject(target);
            var prop = so.FindProperty("scenario");
            if (prop != null) { prop.objectReferenceValue = comp; so.ApplyModifiedProperties(); }

            Selection.activeObject = go;
        }

        void CreateAndAssignStatsUI()
        {
            var parent = EnsureManagersRoot();
            if (!parent) return;

            var uiType = FindType("StatsUIController", "Pitech.XR.Stats");
            if (uiType == null)
            {
                EditorUtility.DisplayDialog("Stats UI not found",
                    "Could not find Pitech.XR.Stats.StatsUIController.", "OK");
                return;
            }

            var go = new GameObject("StatsUIController");
            Undo.RegisterCreatedObjectUndo(go, "Create StatsUIController");
            var comp = go.AddComponent(uiType) as Component;
            go.transform.SetParent(parent, false);

            var so = new SerializedObject(target);
            var prop = so.FindProperty("statsUI");
            if (prop != null) { prop.objectReferenceValue = comp; so.ApplyModifiedProperties(); }

            Selection.activeObject = go;
        }

        void CreateAndAssignStatsConfig()
        {
            const string folder = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/StatsConfig.asset");

            var configType = FindType("StatsConfig", "Pitech.XR.Stats");
            if (configType == null)
            {
                EditorUtility.DisplayDialog("Stats Config not found",
                    "Could not find Pitech.XR.Stats.StatsConfig.", "OK");
                return;
            }

            var asset = ScriptableObject.CreateInstance(configType);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var so = new SerializedObject(target);
            var prop = so.FindProperty("statsConfig");
            if (prop != null) { prop.objectReferenceValue = asset; so.ApplyModifiedProperties(); }
            EditorGUIUtility.PingObject(asset);
        }

        // Tiny caption above a row (e.g., "UI Controller", "Config").
        static void MiniCaption(string text)
        {
            var r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            r.y += 2f;
            EditorGUI.LabelField(r, text, EditorStyles.miniLabel);
        }

        // Object field with right-aligned Ping / Clear. No label.
        // Object field with right-aligned Ping / Clear. No label ever.
        static void ObjectFieldWithPingClear(
    SerializedObject owner,
    SerializedProperty prop,
    string simpleTypeName = null,
    string ns = null)
        {
            const float clearW = 54f;
            const float pingW = 50f;
            const float pad = 4f;

            var line = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            var clear = new Rect(line.xMax - clearW, line.y, clearW, line.height);
            var ping = new Rect(clear.x - pad - pingW, line.y, pingW, line.height);
            var field = new Rect(line.x, line.y, ping.x - pad - line.x, line.height);

            // Try to resolve the type by name; if missing package, use Object so we still compile.
            Type objectType = typeof(UnityEngine.Object);
            if (!string.IsNullOrEmpty(simpleTypeName))
            {
                var t = FindType(simpleTypeName, ns);
                if (t != null) objectType = t;
            }

            EditorGUI.BeginChangeCheck();
            var newObj = EditorGUI.ObjectField(field, GUIContent.none, prop.objectReferenceValue, objectType, true);
            if (EditorGUI.EndChangeCheck())
            {
                prop.objectReferenceValue = newObj;
                owner.ApplyModifiedProperties();
            }

            using (new EditorGUI.DisabledScope(prop.objectReferenceValue == null))
                if (GUI.Button(ping, "Ping")) EditorGUIUtility.PingObject(prop.objectReferenceValue);

            if (GUI.Button(clear, "Clear"))
            {
                prop.objectReferenceValue = null;
                owner.ApplyModifiedProperties();
            }
        }



    }
}
#endif
