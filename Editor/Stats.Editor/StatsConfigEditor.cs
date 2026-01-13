#if UNITY_EDITOR
using System.Collections.Generic;
using Pitech.XR.Stats;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Pitech.XR.Stats.Editor
{
    [CustomEditor(typeof(StatsConfig))]
    public sealed class StatsConfigEditor : UnityEditor.Editor
    {
        SerializedProperty _entriesProp;
        ReorderableList _list;

        static readonly GUIContent KeyLabel = new GUIContent("Title", "Stat title / identifier used in code & bindings. Must be unique. Example: Money, CO2, Adoption.");
        static readonly GUIContent DefaultLabel = new GUIContent("Default", "Initial value when a scenario starts / stats are reset.");
        static readonly GUIContent MinLabel = new GUIContent("Min", "Minimum allowed value (used for UI sliders/clamping).");
        static readonly GUIContent MaxLabel = new GUIContent("Max", "Maximum allowed value (used for UI sliders/clamping).");

        void OnEnable()
        {
            _entriesProp = serializedObject.FindProperty("entries");

            _list = new ReorderableList(serializedObject, _entriesProp, draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true);
            _list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Stats");
            _list.elementHeightCallback = _ => EditorGUIUtility.singleLineHeight * 4f + 14f;
            _list.drawElementCallback = DrawElement;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Stats Config defines which stats exist (any number), their default values, and their min/max ranges.\n\n" +
                "How to use:\n" +
                "1) Create entries here (unique Titles).\n" +
                "2) Assign this asset to your Stats UI Controller (Editor Config).\n" +
                "3) Bind UI to Titles (text/slider). At runtime, systems modify stats by Title.",
                MessageType.Info);

            _list.DoLayoutList();

            DrawValidation();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var el = _entriesProp.GetArrayElementAtIndex(index);
            if (el == null) return;

            var keyProp = el.FindPropertyRelative("key");
            var defProp = el.FindPropertyRelative("defaultValue");
            var minProp = el.FindPropertyRelative("min");
            var maxProp = el.FindPropertyRelative("max");

            rect.y += 2;
            float line = EditorGUIUtility.singleLineHeight;
            float gap = 2f;

            var r0 = new Rect(rect.x, rect.y, rect.width, line);
            var r1 = new Rect(rect.x, rect.y + (line + gap) * 1, rect.width, line);
            var r2 = new Rect(rect.x, rect.y + (line + gap) * 2, rect.width, line);
            var r3 = new Rect(rect.x, rect.y + (line + gap) * 3, rect.width, line);

            EditorGUI.PropertyField(r0, keyProp, KeyLabel);
            EditorGUI.PropertyField(r1, defProp, DefaultLabel);
            EditorGUI.PropertyField(r2, minProp, MinLabel);
            EditorGUI.PropertyField(r3, maxProp, MaxLabel);
        }

        void DrawValidation()
        {
            if (_entriesProp == null) return;

            var seen = new HashSet<string>();
            var dupes = new List<string>();
            for (int i = 0; i < _entriesProp.arraySize; i++)
            {
                var el = _entriesProp.GetArrayElementAtIndex(i);
                var keyProp = el?.FindPropertyRelative("key");
                var k = StatsConfig.NormalizeKey(keyProp?.stringValue);
                if (string.IsNullOrEmpty(k)) continue;
                if (!seen.Add(k)) dupes.Add(k);
            }

            if (dupes.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Duplicate Keys found: " + string.Join(", ", dupes) + "\nKeys must be unique.",
                    MessageType.Error);
            }
        }
    }
}
#endif


