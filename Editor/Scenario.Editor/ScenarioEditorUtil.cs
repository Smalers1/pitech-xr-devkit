#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Scenario.Editor
{
    internal static class ScenarioEditorUtil
    {
        /// Ensure every step has a stable GUID. Marks the Scenario dirty if it fixes any.
        public static bool EnsureStableGuids(UnityEngine.Object scenarioObj)
        {
            if (scenarioObj == null) return false;

            var so = new SerializedObject(scenarioObj);
            var steps = so.FindProperty("steps");
            if (steps == null || !steps.isArray) return false;

            bool changed = false;
            for (int i = 0; i < steps.arraySize; i++)
            {
                var el = steps.GetArrayElementAtIndex(i);
                if (el == null || el.managedReferenceValue == null) continue;

                var guidProp = el.FindPropertyRelative("guid");
                if (guidProp == null) continue;

                if (string.IsNullOrEmpty(guidProp.stringValue))
                {
                    guidProp.stringValue = System.Guid.NewGuid().ToString();
                    changed = true;
                }
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(scenarioObj);
            }
            return changed;
        }

        /// Get the managed-reference element for a step by index.
        public static SerializedProperty GetStepProperty(SerializedObject scenarioSO, int stepIndex)
        {
            var steps = scenarioSO.FindProperty("steps");
            return (steps != null && stepIndex >= 0 && stepIndex < steps.arraySize)
                ? steps.GetArrayElementAtIndex(stepIndex)
                : null;
        }
    }
}
#endif
