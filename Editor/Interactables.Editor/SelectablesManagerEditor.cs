#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Pitech.XR.Interactables;

[CustomEditor(typeof(SelectablesManager))]
public class SelectablesManagerEditor : Editor
{
    // Mode
    SerializedProperty _mode, _forceDesktopOnMobile, _xrIsVrOnlyIfMetaPresent;

    // Catalog
    SerializedProperty _collectRoot, _auto, _layers, _items;

    // Visuals
    SerializedProperty _tintSelected, _tintColor, _useEmission;

    void OnEnable()
    {
        _mode = serializedObject.FindProperty("mode");
        _forceDesktopOnMobile = serializedObject.FindProperty("forceDesktopOnMobile");
        _xrIsVrOnlyIfMetaPresent = serializedObject.FindProperty("xrIsVrOnlyIfMetaPresent");

        _collectRoot = serializedObject.FindProperty("collectRoot");
        _auto = serializedObject.FindProperty("autoCollectInChildren");
        _layers = serializedObject.FindProperty("selectableLayers");
        _items = serializedObject.FindProperty("items");

        _tintSelected = serializedObject.FindProperty("tintSelected");
        _tintColor = serializedObject.FindProperty("tintColor");
        _useEmission = serializedObject.FindProperty("useEmission");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Selectables Manager (Meta VR + AR)\n" +
            "• Desktop/AR: pointer events go through EventSystem + PhysicsRaycaster.\n" +
            "  Each item's collider auto-receives a SelectableTarget component.\n" +
            "• VR: bind Meta Event Wrapper OnSelect → MetaSelect(GameObject).",
            MessageType.Info);

        DrawSceneRequirementsCheck();

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
                EditorGUILayout.HelpBox(
                    "Desktop/AR picking is delegated to Unity's EventSystem.\n" +
                    "Ensure the scene has an active EventSystem and a PhysicsRaycaster\n" +
                    "on the camera you render through (typically the ARCamera).",
                    MessageType.None);
            }
        }

        // ---------------- Catalog ----------------
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Catalog", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_collectRoot, new GUIContent("Collect Root"));
            EditorGUILayout.PropertyField(_auto, new GUIContent("Auto Collect In Children"));
            EditorGUILayout.PropertyField(_layers, new GUIContent("Selectable Layers"));
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

    // ---------------------------------------------------------------------
    // Scene-wide requirements check: surfaces missing PhysicsRaycaster /
    // EventSystem directly in the inspector so the developer catches the
    // misconfiguration at authoring time instead of discovering it as
    // "interactions don't work" on device.
    // ---------------------------------------------------------------------
    void DrawSceneRequirementsCheck()
    {
        var modeEnum = (SelectablesManager.PlatformMode)_mode.enumValueIndex;
        if (modeEnum == SelectablesManager.PlatformMode.ForceVRMeta) return;

#if UNITY_2023_1_OR_NEWER
        var raycaster = Object.FindFirstObjectByType<PhysicsRaycaster>(FindObjectsInactive.Include);
        var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
#else
        var raycaster = Object.FindObjectOfType<PhysicsRaycaster>(true);
        var eventSystem = Object.FindObjectOfType<EventSystem>(true);
#endif

        if (raycaster == null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(
                    "Missing PhysicsRaycaster. Add a PhysicsRaycaster component to " +
                    "your ARCamera (or main Camera) — without it, IPointerDownHandler " +
                    "cannot dispatch to 3D colliders and no selections will register.",
                    MessageType.Error);

                if (GUILayout.Button("Add to Main Camera", GUILayout.Width(160), GUILayout.Height(38)))
                {
                    var cam = Camera.main;
                    if (cam)
                    {
                        Undo.AddComponent<PhysicsRaycaster>(cam.gameObject);
                        EditorUtility.SetDirty(cam.gameObject);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog(
                            "No Main Camera",
                            "No Camera tagged 'MainCamera' was found. " +
                            "Add a PhysicsRaycaster manually to your ARCamera.",
                            "OK");
                    }
                }
            }
        }

        if (eventSystem == null)
        {
            EditorGUILayout.HelpBox(
                "Missing EventSystem. Add an EventSystem GameObject " +
                "(GameObject ▸ UI ▸ Event System) with an Input System UI Input " +
                "Module (or StandaloneInputModule on the legacy input handling).",
                MessageType.Error);
        }
    }
}
#endif
