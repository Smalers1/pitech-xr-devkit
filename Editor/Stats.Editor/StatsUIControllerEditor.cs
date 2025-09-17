#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Pitech.XR.Stats; // Runtime namespace

namespace Pitech.XR.Stats.Editor
{
    [CustomEditor(typeof(StatsUIController))]
    public class StatsUIControllerEditor : UnityEditor.Editor
    {
        SerializedProperty _editorConfigProp;   // "editorConfig"
        SerializedProperty _bindingsProp;       // "bindings"

        // cache so we can notice config changes
        int _lastCfgId = 0;

        void OnEnable()
        {
            _editorConfigProp = serializedObject.FindProperty("editorConfig");
            _bindingsProp = serializedObject.FindProperty("bindings");

            // First time opening: if there is a config and bindings are empty -> build
            var cfg = _editorConfigProp?.objectReferenceValue as StatsConfig;
            if (cfg && (_bindingsProp == null || _bindingsProp.arraySize == 0))
            {
                RebuildBindingsFromConfig(cfg);
                _lastCfgId = cfg.GetInstanceID();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var cfg = _editorConfigProp?.objectReferenceValue as StatsConfig;

            DrawHeaderHelp();

            // ================= Config row =================
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Editor Config", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(_editorConfigProp, GUIContent.none);
                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();
                        cfg = _editorConfigProp.objectReferenceValue as StatsConfig;
                        if (cfg)
                        {
                            RebuildBindingsFromConfig(cfg);
                            _lastCfgId = cfg.GetInstanceID();
                        }
                        else
                        {
                            // cleared -> keep user list as-is
                        }
                    }

                    using (new EditorGUI.DisabledScope(cfg == null))
                    {
                        if (GUILayout.Button("Sync from Config", GUILayout.Width(140)))
                        {
                            RebuildBindingsFromConfig(cfg);
                            _lastCfgId = cfg ? cfg.GetInstanceID() : 0;
                        }
                    }
                }

                if (cfg == null)
                {
                    EditorGUILayout.HelpBox(
                        "Assign a StatsConfig. All stats it contains will be added below automatically as bindings.\n" +
                        "You only need to wire the Text and/or Slider references.", MessageType.Info);
                }
            }

            // Rebuild automatically if the assigned asset changed on disk (rare, but handy)
            if (cfg && _lastCfgId != cfg.GetInstanceID())
            {
                RebuildBindingsFromConfig(cfg);
                _lastCfgId = cfg.GetInstanceID();
            }

            // ================= Bindings =================
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Bindings", EditorStyles.boldLabel);

            if (_bindingsProp == null || _bindingsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No bindings yet. Assign a StatsConfig above to auto-create them.", MessageType.None);
            }
            else
            {
                // Create a quick lookup for per-key max (and optionally default)
                Dictionary<StatKey, float> maxByKey = null;
                if (cfg != null)
                {
                    maxByKey = new Dictionary<StatKey, float>();
                    foreach (var kv in cfg.All())
                        maxByKey[kv.Key] = kv.Value.max;
                }

                for (int i = 0; i < _bindingsProp.arraySize; i++)
                {
                    var el = _bindingsProp.GetArrayElementAtIndex(i);
                    if (el == null) continue;

                    var keyProp = el.FindPropertyRelative("key");
                    var textProp = el.FindPropertyRelative("text");
                    var sliderProp = el.FindPropertyRelative("slider");
                    var fmtProp = el.FindPropertyRelative("format");

                    var key = (StatKey)keyProp.enumValueIndex;

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        // Header
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(key.ToString()), EditorStyles.boldLabel);
                            GUILayout.FlexibleSpace();
                            if (cfg != null && maxByKey != null && maxByKey.TryGetValue(key, out float maxVal))
                            {
                                using (new EditorGUI.DisabledScope(true))
                                {
                                    EditorGUILayout.FloatField(new GUIContent("Max (from Config)"), maxVal, GUILayout.MaxWidth(220));
                                }
                            }
                        }

                        // Fields (Key locked to match config)
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.PropertyField(keyProp, new GUIContent("Key"));

                        EditorGUILayout.PropertyField(textProp, new GUIContent("Text (TMP)"));
                        EditorGUILayout.PropertyField(sliderProp, new GUIContent("Slider"));
                        EditorGUILayout.PropertyField(fmtProp, new GUIContent("Format"), true);

                        // Tiny hint
                        EditorGUILayout.LabelField("Tip: Leave fields you don’t use empty. For example, a stat can drive only text or only a slider.", EditorStyles.miniLabel);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ---------- helpers ----------

        void DrawHeaderHelp()
        {
            EditorGUILayout.HelpBox(
                "Bind your visual UI to stat keys.\n\n" +
                "• Assign a StatsConfig and the list of bindings will be created for you.\n" +
                "• Wire a Text (TMP) and/or a Slider for each stat you want shown.\n" +
                "• Slider range uses the Max value from the StatsConfig automatically.\n" +
                "• Values are pushed at startup and animate on change.",
                MessageType.Info);
        }

        void RebuildBindingsFromConfig(StatsConfig cfg)
        {
            if (cfg == null || _bindingsProp == null) return;

            // Remember existing object refs by key to avoid losing wiring when re-syncing
            var keep = new Dictionary<StatKey, (Object text, Slider slider, string fmt)>();
            for (int i = 0; i < _bindingsProp.arraySize; i++)
            {
                var el = _bindingsProp.GetArrayElementAtIndex(i);
                var key = (StatKey)el.FindPropertyRelative("key").enumValueIndex;
                var text = el.FindPropertyRelative("text").objectReferenceValue;
                var slid = el.FindPropertyRelative("slider").objectReferenceValue as Slider;
                var fmt = el.FindPropertyRelative("format").stringValue;
                keep[key] = (text, slid, fmt);
            }

            _bindingsProp.ClearArray();

            int idx = 0;
            foreach (var kv in cfg.All())
            {
                var key = kv.Key;

                _bindingsProp.InsertArrayElementAtIndex(idx);
                var el = _bindingsProp.GetArrayElementAtIndex(idx);

                el.FindPropertyRelative("key").enumValueIndex = (int)key;

                // restore previous wiring if we had it
                if (keep.TryGetValue(key, out var saved))
                {
                    el.FindPropertyRelative("text").objectReferenceValue = saved.text;
                    el.FindPropertyRelative("slider").objectReferenceValue = saved.slider;
                    el.FindPropertyRelative("format").stringValue = string.IsNullOrEmpty(saved.fmt) ? "N0" : saved.fmt;
                }
                else
                {
                    el.FindPropertyRelative("text").objectReferenceValue = null;
                    el.FindPropertyRelative("slider").objectReferenceValue = null;
                    el.FindPropertyRelative("format").stringValue = "N0";
                }

                idx++;
            }

            serializedObject.ApplyModifiedProperties();

            // Nice toast in console to confirm
            Debug.Log($"[StatsUIController] Synced {idx} binding(s) from StatsConfig \"{cfg.name}\".", target);
        }
    }
}
#endif
