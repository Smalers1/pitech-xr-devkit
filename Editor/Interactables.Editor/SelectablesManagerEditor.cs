#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Pitech.XR.Interactables;

[CustomEditor(typeof(SelectablesManager))]
public class SelectablesManagerEditor : Editor
{
    // Mode
    SerializedProperty _mode;

    // Catalog
    SerializedProperty _collectRoot, _auto, _layers, _trigger, _items;

    // Desktop/Mobile picking (στο VR αγνοείται)
    SerializedProperty _pickingEnabled, _rayLength, _ignoreUI;

    // Visuals
    SerializedProperty _tintSelected, _tintColor, _useEmission;

    void OnEnable()
    {
        _mode = serializedObject.FindProperty("mode");

        // Catalog
        _collectRoot = serializedObject.FindProperty("collectRoot");
        _auto = serializedObject.FindProperty("autoCollectInChildren");
        _layers = serializedObject.FindProperty("selectableLayers");
        _trigger = serializedObject.FindProperty("triggerHits");
        _items = serializedObject.FindProperty("items");

        // Picking (Desktop/Mobile)
        _pickingEnabled = serializedObject.FindProperty("pickingEnabled");
        _rayLength = serializedObject.FindProperty("rayLength");
        _ignoreUI = serializedObject.FindProperty("ignoreUI");

        // Visuals
        _tintSelected = serializedObject.FindProperty("tintSelected");
        _tintColor = serializedObject.FindProperty("tintColor");
        _useEmission = serializedObject.FindProperty("useEmission");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Selectables Manager (Meta VR Ready)\n" +
            "• VR (Meta): Χρήση Ray Interactor + Event Wrappers → κάλεσε MetaSelect/MetaUnselect.\n" +
            "• Desktop/Mobile: Click-to-select με Camera.ScreenPointToRay.\n" +
            "• Δεν χρειάζονται custom raycasts στο VR.",
            MessageType.Info);

        // ---------------- Mode & Input ----------------
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Mode & Input", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_mode, new GUIContent("Platform Mode"));

            var modeEnum = (SelectablesManager.PlatformMode)_mode.enumValueIndex;

            if (modeEnum == SelectablesManager.PlatformMode.ForceVRMeta)
            {
                EditorGUILayout.HelpBox(
                    "VR (Meta) mode: Η επιλογή γίνεται από τα Meta Event Wrappers.\n" +
                    "Δέσε στα UnityEvents:\n" +
                    " • OnSelect → SelectablesManager.MetaSelect(GameObject)\n" +
                    " • (προαιρετικό) OnUnselect → SelectablesManager.MetaUnselect(GameObject)",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.PropertyField(_pickingEnabled, new GUIContent("Picking Enabled (Desktop/Mobile)"));
                EditorGUILayout.PropertyField(_rayLength, new GUIContent("Ray Length (Desktop/Mobile)"));
                EditorGUILayout.PropertyField(_ignoreUI, new GUIContent("Ignore UI (EventSystem)"));
            }
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
            EditorGUILayout.PropertyField(_items, includeChildren: true);

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
