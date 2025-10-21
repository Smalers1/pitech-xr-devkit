#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Pitech.XR.Interactables;

[CustomEditor(typeof(SelectablesManager))]
public class SelectablesManagerEditor : Editor
{
    // Catalog
    SerializedProperty _collectRoot, _auto, _layers, _trigger, _items;

    // Picking
    SerializedProperty _pickingEnabled, _raySource, _rayTransform, _rayLength, _ignoreUI;
#if ENABLE_INPUT_SYSTEM
    SerializedProperty _selectAction;
#endif

    // Visuals
    SerializedProperty _tintSelected, _tintColor, _useEmission;

    void OnEnable()
    {
        // Catalog
        _collectRoot = serializedObject.FindProperty("collectRoot");
        _auto = serializedObject.FindProperty("autoCollectInChildren");
        _layers = serializedObject.FindProperty("selectableLayers");
        _trigger = serializedObject.FindProperty("triggerHits");
        _items = serializedObject.FindProperty("items");

        // Picking
        _pickingEnabled = serializedObject.FindProperty("pickingEnabled");
        _raySource = serializedObject.FindProperty("raySource");
        _rayTransform = serializedObject.FindProperty("rayTransform");
        _rayLength = serializedObject.FindProperty("rayLength");
        _ignoreUI = serializedObject.FindProperty("ignoreUI");
#if ENABLE_INPUT_SYSTEM
        _selectAction   = serializedObject.FindProperty("selectAction");
#endif

        // Visuals
        _tintSelected = serializedObject.FindProperty("tintSelected");
        _tintColor = serializedObject.FindProperty("tintColor");
        _useEmission = serializedObject.FindProperty("useEmission");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Selectables Manager\n" +
            "• No components on objects needed.\n" +
            "• Clicks are raycasted against the listed Colliders.\n" +
            "• Optional highlight object per entry is toggled when selected.\n" +
            "• Use ‘Collect From Children’ to bulk grab colliders.",
            MessageType.Info);

        // ---------------- Picking ----------------
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Picking (Input & Ray)", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_pickingEnabled, new GUIContent("Picking Enabled"));
            EditorGUILayout.PropertyField(_raySource, new GUIContent("Ray Source"));

            var source = (SelectablesManager.RaycastSource)_raySource.enumValueIndex;
            switch (source)
            {
                case SelectablesManager.RaycastSource.TransformRay:
                    EditorGUILayout.PropertyField(_rayTransform, new GUIContent("Ray Transform"));
                    EditorGUILayout.PropertyField(_rayLength, new GUIContent("Ray Length"));
                    break;

                case SelectablesManager.RaycastSource.CameraScreenPoint:
                    EditorGUILayout.LabelField("Ray", "Camera.main ScreenPoint", EditorStyles.miniLabel);
                    break;

                case SelectablesManager.RaycastSource.External:
                    EditorGUILayout.HelpBox("External: call TriggerWithRay(ray) from your XR/OVR pointer.", MessageType.None);
                    EditorGUILayout.PropertyField(_rayLength, new GUIContent("Ray Length"));
                    break;
            }

#if ENABLE_INPUT_SYSTEM
            EditorGUILayout.PropertyField(_selectAction, new GUIContent("Select Action (Input System)"));
#else
            EditorGUILayout.HelpBox(
                "Legacy Input in use. To bind XR trigger via the Input System, install/enable the Input System package and set Player → Active Input Handling to Both or Input System.",
                MessageType.None);
#endif
            EditorGUILayout.PropertyField(_ignoreUI, new GUIContent("Ignore UI (EventSystem)"));
        }

        // ---------------- Catalog ----------------
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Catalog", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_collectRoot, new GUIContent("Collect Root"));
            EditorGUILayout.PropertyField(_auto, new GUIContent("Auto Collect In Children"));
            EditorGUILayout.PropertyField(_layers, new GUIContent("Selectable Layers"));
            EditorGUILayout.PropertyField(_trigger, new GUIContent("Query Triggers"));
        }

        // ---------------- Entries ----------------
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
                    _items.ClearArray();
            }
        }

        // ---------------- Visuals ----------------
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Visuals", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_tintSelected, new GUIContent("Tint Selected"));
            if (_tintSelected.boolValue)
            {
                EditorGUILayout.PropertyField(_tintColor, new GUIContent("Tint Color"));
                EditorGUILayout.PropertyField(_useEmission, new GUIContent("Use Emission"));
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
