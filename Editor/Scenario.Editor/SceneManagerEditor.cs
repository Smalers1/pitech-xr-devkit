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
        SerializedProperty _selectablesProp;    // Pitech.XR.Interactables.SelectablesManager
        SerializedProperty _selectionListsProp; // Pitech.XR.Interactables.SelectionLists
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
            _selectablesProp = serializedObject.FindProperty("selectables");
            _selectionListsProp = serializedObject.FindProperty("selectionLists");

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
            
            EditorGUILayout.Space(6);
            DrawInteractablesFeature();

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

                MiniCaption("Scenario");
                ObjectFieldWithPingClear(serializedObject, _scenarioProp, undoName: "Assign Scenario", simpleTypeName: "Scenario", ns: "Pitech.XR.Scenario");

                if (!hasScenario)
                {
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Create & assign Scenario", GUILayout.Height(22)))
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

                // UI Controller row
                MiniCaption("UI Controller");
                ObjectFieldWithPingClear(serializedObject, _statsUIProp, undoName: "Assign Stats UI", simpleTypeName: "StatsUIController", ns: "Pitech.XR.Stats");
                if (!hasUI)
                {
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Create & assign StatsUIController", GUILayout.Height(22)))
                        CreateAndAssignStatsUI();
                }

                // Config row
                MiniCaption("Config");
                ObjectFieldWithPingClear(serializedObject, _statsConfigProp, undoName: "Assign Stats Config", simpleTypeName: "StatsConfig", ns: "Pitech.XR.Stats");
                if (!hasConfig)
                {
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Create & assign StatsConfig asset", GUILayout.Height(22)))
                        CreateAndAssignStatsConfig();
                }
            }
        }

        void DrawInteractablesFeature()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Interactables", TitleStyle);

                bool hasSelMgr = _selectablesProp != null && _selectablesProp.objectReferenceValue != null;
                bool hasSelList = _selectionListsProp != null && _selectionListsProp.objectReferenceValue != null;

                // Selectables Manager row
                MiniCaption("Selectables Manager");
                ObjectFieldWithPingClear(serializedObject, _selectablesProp, undoName: "Assign Selectables Manager", simpleTypeName: "SelectablesManager", ns: "Pitech.XR.Interactables");
                if (!hasSelMgr)
                {
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Create & assign Selectables Manager", GUILayout.Height(22)))
                        CreateAndAssignSelectablesManager();
                }

                // Selection Lists row
                MiniCaption("Selection Lists");
                ObjectFieldWithPingClear(serializedObject, _selectionListsProp, undoName: "Assign Selection Lists", simpleTypeName: "SelectionLists", ns: "Pitech.XR.Interactables");
                if (!hasSelList)
                {
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Create & assign Selection Lists", GUILayout.Height(22)))
                        CreateAndAssignSelectionLists();
                }
            }
        }

        void CreateAndAssignSelectablesManager()
        {
            var parent = EnsureManagersRoot();
            if (!parent) return;

            var t = FindType("SelectablesManager", "Pitech.XR.Interactables");
            if (t == null) { EditorUtility.DisplayDialog("Interactables", "Type Pitech.XR.Interactables.SelectablesManager not found.", "OK"); return; }

            var go = new GameObject("Selectables Manager");
            Undo.RegisterCreatedObjectUndo(go, "Create Selectables Manager");
            var comp = go.AddComponent(t) as Component;
            go.transform.SetParent(parent, false);

            AssignSceneObjectProperty(_selectablesProp, comp, "Assign Selectables Manager");
            Selection.activeObject = go;
        }

        void CreateAndAssignSelectionLists()
        {
            var parent = EnsureManagersRoot();
            if (!parent) return;

            var t = FindType("SelectionLists", "Pitech.XR.Interactables");
            if (t == null) { EditorUtility.DisplayDialog("Interactables", "Type Pitech.XR.Interactables.SelectionLists not found.", "OK"); return; }

            var go = new GameObject("Selection Lists");
            Undo.RegisterCreatedObjectUndo(go, "Create Selection Lists");
            var comp = go.AddComponent(t) as Component;
            go.transform.SetParent(parent, false);

            // If Scene Manager already has a SelectablesManager, auto-link it
            var sm = (Pitech.XR.Scenario.SceneManager)target;
            var lists = comp as Pitech.XR.Interactables.SelectionLists;
            if (lists && sm.selectables) lists.selectables = sm.selectables;

            AssignSceneObjectProperty(_selectionListsProp, comp, "Assign Selection Lists");
            Selection.activeObject = go;
        }

        static Pitech.XR.Scenario.Scenario GetScenarioFromManager(Pitech.XR.Scenario.SceneManager gm)
        {
            if (gm == null) return null;

            var scField = gm.GetType().GetField("scenario",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return scField != null
                ? scField.GetValue(gm) as Pitech.XR.Scenario.Scenario
                : null;
        }

        // --------------------------------------------------------------------
        // Overview (restored)
        // --------------------------------------------------------------------
        void DrawScenarioOverview(Pitech.XR.Scenario.SceneManager gm)
        {
            var sc = GetScenarioFromManager(gm);
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
                    if (s == null)
                    {
                        EditorGUILayout.LabelField($"{i:00}. <null>");
                        continue;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (s is Pitech.XR.Scenario.TimelineStep tl)
                        {
                            var ok = tl.director ? "✓" : "✗";
                            EditorGUILayout.LabelField($"{i:00}. Timeline {ok}", GUILayout.Width(170));
                            EditorGUILayout.ObjectField(tl.director, typeof(PlayableDirector), true);
                            if (!tl.director) EditorGUILayout.HelpBox("Director not set", MessageType.Warning);
                        }
                        else if (s is Pitech.XR.Scenario.CueCardsStep cc)
                        {
                            var times = cc.cueTimes != null ? cc.cueTimes.Length : 0;
                            EditorGUILayout.LabelField($"{i:00}. Cue Cards", GUILayout.Width(170));
                            EditorGUILayout.LabelField(times == 0 ? "tap-only" : $"{times} cue time(s)");
                        }
                        else if (s is Pitech.XR.Scenario.QuestionStep q)
                        {
                            int btns = q.choices?.Count ?? 0;
                            EditorGUILayout.LabelField($"{i:00}. Question", GUILayout.Width(170));
                            EditorGUILayout.LabelField($"Buttons {btns}");
                        }
                        else if (s is Pitech.XR.Scenario.SelectionStep sel)
                        {
                            EditorGUILayout.LabelField($"{i:00}. Selection", GUILayout.Width(170));
                            var mode = sel.completion.ToString();
                            EditorGUILayout.LabelField($"{mode} / Required {sel.requiredSelections}");
                        }
                        else if (s is Pitech.XR.Scenario.InsertStep ins)
                        {
                            EditorGUILayout.LabelField($"{i:00}. Insert", GUILayout.Width(170));
                            string itemName = ins.item ? ins.item.name : "no item";
                            string targetName = ins.targetTrigger ? ins.targetTrigger.name : "no target";
                            EditorGUILayout.LabelField($"{itemName} → {targetName}");
                        }
                        else if (s is Pitech.XR.Scenario.EventStep ev)
                        {
                            EditorGUILayout.LabelField($"{i:00}. Event", GUILayout.Width(170));
                            string waitTxt = ev.waitSeconds > 0f
                                ? $"wait {ev.waitSeconds:0.##}s then next"
                                : "no wait, immediate next";
                            EditorGUILayout.LabelField(waitTxt);
                        }
                        else
                        {
                            // Fallback για οποιοδήποτε μελλοντικό step type
                            EditorGUILayout.LabelField($"{i:00}. {s.GetType().Name}");
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
            {
                var sc = GetScenarioFromManager(gm);
                int totalSteps = (sc != null && sc.steps != null) ? sc.steps.Count : 0;

                // Τρέχον index
                int currentIndex = -1;
                var idxProp = gm.GetType().GetProperty("StepIndex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (idxProp != null)
                {
                    try
                    {
                        var val = idxProp.GetValue(gm, null);
                        if (val is int i) currentIndex = i;
                    }
                    catch { }
                }

                // Γραμμή κατάστασης
                string status;
                if (!Application.isPlaying)
                    status = "Editor idle (enter Play mode)";
                else if (currentIndex < 0 || totalSteps == 0)
                    status = "Idle / finished";
                else
                    status = $"Step {currentIndex + 1} of {totalSteps}";

                EditorGUILayout.LabelField(status);

                // Progress bar (μόνο αν έχουμε valid steps)
                if (totalSteps > 0)
                {
                    float progress = 0f;
                    if (currentIndex >= 0 && currentIndex < totalSteps)
                        progress = (currentIndex + 1) / (float)totalSteps;

                    var rect = GUILayoutUtility.GetRect(18, 18);
                    EditorGUI.ProgressBar(rect, progress, $"{Mathf.RoundToInt(progress * 100f)}%");
                }

                EditorGUILayout.Space(4);

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    var restart = gm.GetType().GetMethod("Restart",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (GUILayout.Button("Restart Scenario") && restart != null)
                        restart.Invoke(gm, null);

                    if (!Application.isPlaying)
                        EditorGUILayout.HelpBox("Enter Play mode to see live progress and restart.", MessageType.None);
                }
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

            AssignSceneObjectProperty(_scenarioProp, comp, "Assign Scenario");

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

            AssignSceneObjectProperty(_statsUIProp, comp, "Assign Stats UI");

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

            AssignSceneObjectProperty(_statsConfigProp, asset, "Assign Stats Config");
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
    string undoName,
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

            var undoTarget = owner?.targetObject;
            EditorGUI.BeginChangeCheck();
            var newObj = EditorGUI.ObjectField(field, GUIContent.none, prop.objectReferenceValue, objectType, true);
            if (EditorGUI.EndChangeCheck())
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, undoName);
                prop.objectReferenceValue = newObj;
                owner.ApplyModifiedProperties();

                if (undoTarget != null)
                {
                    EditorUtility.SetDirty(undoTarget);
                    if (undoTarget is Component c) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                }
            }

            using (new EditorGUI.DisabledScope(prop.objectReferenceValue == null))
                if (GUI.Button(ping, "Ping")) EditorGUIUtility.PingObject(prop.objectReferenceValue);

            if (GUI.Button(clear, "Clear"))
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, undoName);
                prop.objectReferenceValue = null;
                owner.ApplyModifiedProperties();

                if (undoTarget != null)
                {
                    EditorUtility.SetDirty(undoTarget);
                    if (undoTarget is Component c) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                }
            }
        }

        void AssignSceneObjectProperty(SerializedProperty prop, UnityEngine.Object value, string undoName)
        {
            if (prop == null) return;

            Undo.RecordObject(target, undoName);
            prop.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            if (target is Component c) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        }



    }
}
#endif
