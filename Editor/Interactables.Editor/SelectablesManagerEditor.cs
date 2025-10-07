#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Pitech.XR.Interactables;

[CustomEditor(typeof(SelectablesManager))]
public class SelectablesManagerEditor : Editor
{
    SerializedProperty _collectRoot, _auto, _layers, _trigger, _items;

    void OnEnable()
    {
        _collectRoot = serializedObject.FindProperty("collectRoot");
        _auto = serializedObject.FindProperty("autoCollectInChildren");
        _layers = serializedObject.FindProperty("selectableLayers");
        _trigger = serializedObject.FindProperty("triggerHits");
        _items = serializedObject.FindProperty("items");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Selectables Manager\n" +
            "• No components on objects needed.\n" +
            "• Clicks are raycasted against the listed Colliders.\n" +
            "• Optional highlight object per entry is toggled when selected.\n" +
            "• Use ‘Collect From Children’ to bulk grab colliders.", MessageType.Info);

        EditorGUILayout.PropertyField(_collectRoot, new GUIContent("Collect Root"));
        EditorGUILayout.PropertyField(_auto, new GUIContent("Auto Collect In Children"));
        EditorGUILayout.PropertyField(_layers, new GUIContent("Selectable Layers"));
        EditorGUILayout.PropertyField(_trigger, new GUIContent("Query Triggers"));

        EditorGUILayout.Space(6);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_items, true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Collect From Children"))
                {
                    foreach (var t in targets)
                    {
                        var mgr = (SelectablesManager)t;
                        Undo.RecordObject(mgr, "Collect Selectables");
                        mgr.CollectFromChildren();
                        EditorUtility.SetDirty(mgr);
                    }
                }
                if (GUILayout.Button("Clear All"))
                {
                    _items.ClearArray();
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
