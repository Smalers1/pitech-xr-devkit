#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Pitech.XR.Interactables;

[CustomEditor(typeof(SelectablesManager))]
public class SelectablesManagerEditor : Editor
{
    // Mode
    SerializedProperty _mode, _forceDesktopOnMobile, _xrIsVrOnlyIfMetaPresent;

    // Catalog
    SerializedProperty _collectRoot, _auto, _layers, _trigger, _items;

    // Picking (Desktop/Mobile/AR)
    SerializedProperty _pickingEnabled, _rayLength, _ignoreUI, _rayCamera;

    // Visuals
    SerializedProperty _tintSelected, _tintColor, _useEmission;

    void OnEnable()
    {
        // Mode
        _mode = serializedObject.FindProperty("mode");
        _forceDesktopOnMobile = serializedObject.FindProperty("forceDesktopOnMobile");
        _xrIsVrOnlyIfMetaPresent = serializedObject.FindProperty("xrIsVrOnlyIfMetaPresent");

        // Catalog
        _collectRoot = serializedObject.FindProperty("collectRoot");
        _auto = serializedObject.FindProperty("autoCollectInChildren");
        _layers = serializedObject.FindProperty("selectableLayers");
        _trigger = serializedObject.FindProperty("triggerHits");
        _items = serializedObject.FindProperty("items");

        // Picking
        _pickingEnabled = serializedObject.FindProperty("pickingEnabled");
        _ignoreUI = serializedObject.FindProperty("ignoreUI");
        _rayLength = serializedObject.FindProperty("rayLength");
        _rayCamera = serializedObject.FindProperty("rayCamera");

        // Visuals
        _tintSelected = serializedObject.FindProperty("tintSelected");
        _tintColor = serializedObject.FindProperty("tintColor");
        _useEmission = serializedObject.FindProperty("useEmission");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Selectables Manager (Meta VR + AR)\n" +
            "• VR: Use Meta Ray Interactor + Event Wrappers → call MetaSelect/MetaUnselect.\n" +
            "• Desktop/AR: ScreenPointToRay from the Ray Camera (assign your ARCamera).\n" +
            "• UI still works; world taps are blocked only when really over UI.",
            MessageType.Info);

        // ---------------- Mode & Input ----------------
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Mode & Input", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_mode, new GUIContent("Platform Mode"));

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Platform Safeguards", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_forceDesktopOnMobile, new GUIContent("Force Desktop On Mobile"));
            EditorGUILayout.PropertyField(_xrIsVrOnlyIfMetaPresent, new GUIContent("XR is VR Only If Meta Present"));

            var modeEnum = (SelectablesManager.PlatformMode)_mode.enumValueIndex;

            if (modeEnum == SelectablesManager.PlatformMode.ForceVRMeta)
            {
                EditorGUILayout.HelpBox(
                    "VR (Meta): selection comes from Meta Event Wrappers.\n" +
                    "Bind:\n" +
                    "• OnSelect → SelectablesManager.MetaSelect(GameObject)\n" +
                    "• (optional) OnUnselect → SelectablesManager.MetaUnselect(GameObject)",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.PropertyField(_pickingEnabled, new GUIContent("Picking Enabled (Desktop/AR)"));
                EditorGUILayout.PropertyField(_rayCamera, new GUIContent("Ray Camera (ARCamera)"));
                EditorGUILayout.PropertyField(_rayLength, new GUIContent("Ray Length (Desktop/AR)"));
                EditorGUILayout.PropertyField(_ignoreUI, new GUIContent("Ignore UI (EventSystem)"));

                // Quick AR preset
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Apply AR Preset", GUILayout.Width(140)))
                    {
                        _mode.enumValueIndex = (int)SelectablesManager.PlatformMode.Auto;
                        _forceDesktopOnMobile.boolValue = true;
                        _xrIsVrOnlyIfMetaPresent.boolValue = true;
                        if (_rayLength.floatValue < 10000f) _rayLength.floatValue = 10000f;
                        _pickingEnabled.boolValue = true;
                        _ignoreUI.boolValue = true;
                    }
                }
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
