#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Interactables;

namespace Pitech.XR.Interactables.Editor
{
    /// <summary>
    /// Right-click context menu "Pi tech > Make Grabbable".
    /// Adds Grabbable + Rigidbody + Collider and optionally wires Meta Interaction SDK
    /// and Photon Fusion components — all via reflection so the DevKit compiles
    /// without those SDKs installed.
    /// </summary>
    public static class MakeGrabbableWizard
    {
        // ───────── Context Menu ─────────

        [MenuItem("GameObject/Pi tech/Make Grabbable", false, 10)]
        static void MakeGrabbable(MenuCommand cmd)
        {
            var go = cmd.context as GameObject;
            if (!go) return;

            // Prevent running once per selected object in multi-select (Unity quirk)
            if (Selection.gameObjects.Length > 1 && cmd.context != Selection.activeGameObject)
                return;

            // Apply to all selected objects
            foreach (var selected in Selection.gameObjects)
                MakeGrabbableWindow.Open(selected);
        }

        [MenuItem("GameObject/Pi tech/Make Grabbable", true)]
        static bool MakeGrabbableValidate() => Selection.activeGameObject != null;
    }

    /// <summary>Compact wizard window for configuring the grabbable setup.</summary>
    public class MakeGrabbableWindow : EditorWindow
    {
        // ───────── SDK Detection (cached) ─────────
        static bool s_Probed;
        static bool s_HasMetaInteraction;   // Meta Interaction SDK (Oculus.Interaction)
        static bool s_HasFusion;            // Photon Fusion 2

        static Type s_GrabbableType;             // Oculus.Interaction.Grabbable
        static Type s_GrabInteractableType;      // Oculus.Interaction.GrabInteractable
        static Type s_RigidbodyKinematicRefType; // Oculus.Interaction.Rigidbody.RigidbodyKinematicRef — optional
        static Type s_HandGrabInteractableType;  // Oculus.Interaction.HandGrab.HandGrabInteractable — optional

        static Type s_NetworkObjectType;         // Fusion.NetworkObject
        static Type s_NetworkRigidbody3DType;    // Fusion.Addons.Physics.NetworkRigidbody3D or Fusion.NetworkRigidbody3D
        static Type s_NetworkTransformType;      // Fusion.NetworkTransform

        static void ProbeSDKs()
        {
            if (s_Probed) return;
            s_Probed = true;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Meta Interaction SDK
            s_GrabbableType = FindType(assemblies, "Oculus.Interaction.Grabbable");
            s_GrabInteractableType = FindType(assemblies, "Oculus.Interaction.GrabInteractable");
            s_HandGrabInteractableType = FindType(assemblies, "Oculus.Interaction.HandGrab.HandGrabInteractable");
            s_RigidbodyKinematicRefType = FindType(assemblies, "Oculus.Interaction.Rigidbody.RigidbodyKinematicRef");
            s_HasMetaInteraction = s_GrabbableType != null;

            // Photon Fusion
            s_NetworkObjectType = FindType(assemblies, "Fusion.NetworkObject");
            s_NetworkRigidbody3DType = FindType(assemblies, "Fusion.Addons.Physics.NetworkRigidbody3D")
                                   ?? FindType(assemblies, "Fusion.NetworkRigidbody3D");
            s_NetworkTransformType = FindType(assemblies, "Fusion.NetworkTransform");
            s_HasFusion = s_NetworkObjectType != null;
        }

        static Type FindType(System.Reflection.Assembly[] assemblies, string fullName)
        {
            foreach (var asm in assemblies)
            {
                var t = asm.GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }

        // ───────── Instance State ─────────
        GameObject _target;
        bool _addMeta = true;
        bool _addHandGrab = true;
        bool _addFusion;
        bool _kinematicWhileGrabbed = true;
        bool _useGravity = true;
        bool _snapBack;
        bool _addColliderIfMissing = true;

        public static void Open(GameObject target)
        {
            ProbeSDKs();
            var win = GetWindow<MakeGrabbableWindow>(utility: true, "Make Grabbable");
            win._target = target;
            win._addMeta = s_HasMetaInteraction;
            win._addFusion = false; // opt-in
            win.minSize = new Vector2(380, 340);
            win.maxSize = new Vector2(420, 500);
            win.ShowUtility();
        }

        void OnGUI()
        {
            if (!_target)
            {
                EditorGUILayout.HelpBox("No target selected.", MessageType.Warning);
                if (GUILayout.Button("Close")) Close();
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Make Grabbable", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Target: {_target.name}", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);

            // ── Physics ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Physics", EditorStyles.boldLabel);
                _useGravity = EditorGUILayout.Toggle("Use Gravity", _useGravity);
                _kinematicWhileGrabbed = EditorGUILayout.Toggle("Kinematic While Grabbed", _kinematicWhileGrabbed);
                _snapBack = EditorGUILayout.Toggle("Snap Back On Release", _snapBack);
                _addColliderIfMissing = EditorGUILayout.Toggle("Add Box Collider (if none)", _addColliderIfMissing);
            }

            EditorGUILayout.Space(4);

            // ── Meta Interaction SDK ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Meta Interaction SDK", EditorStyles.boldLabel);

                if (s_HasMetaInteraction)
                {
                    _addMeta = EditorGUILayout.Toggle("Add Meta Grab Components", _addMeta);
                    using (new EditorGUI.DisabledScope(!_addMeta))
                    {
                        _addHandGrab = EditorGUILayout.Toggle("  + Hand Grab (pinch/palm)", _addHandGrab);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Meta Interaction SDK not detected.\n" +
                        "Install com.meta.xr.sdk.interaction to enable VR grab components.",
                        MessageType.None);
                }
            }

            EditorGUILayout.Space(4);

            // ── Photon Fusion ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Photon Fusion (Multiplayer)", EditorStyles.boldLabel);

                if (s_HasFusion)
                {
                    _addFusion = EditorGUILayout.Toggle("Add Network Components", _addFusion);
                    if (_addFusion)
                    {
                        EditorGUILayout.HelpBox(
                            "Adds NetworkObject + NetworkRigidbody3D (or NetworkTransform).\n" +
                            "You may need to set State Authority after adding.",
                            MessageType.None);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Photon Fusion not detected.\n" +
                        "Install com.exitgames.photon.fusion to enable multiplayer components.",
                        MessageType.None);
                }
            }

            EditorGUILayout.Space(12);

            // ── Apply ──
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                    Close();

                GUILayout.Space(8);

                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.7f, 1f, 1f);
                if (GUILayout.Button("Apply", GUILayout.Width(120), GUILayout.Height(28)))
                {
                    Apply();
                    Close();
                }
                GUI.backgroundColor = prevColor;
            }

            EditorGUILayout.Space(4);
        }

        // ───────── Apply Logic ─────────

        void Apply()
        {
            Undo.SetCurrentGroupName("Make Grabbable");
            int group = Undo.GetCurrentGroup();

            // 1. Collider
            if (_addColliderIfMissing && !_target.GetComponent<Collider>())
            {
                var box = Undo.AddComponent<BoxCollider>(_target);
                // Auto-fit to mesh bounds if available
                FitColliderToBounds(box);
            }

            // 2. Rigidbody
            var rb = _target.GetComponent<Rigidbody>();
            if (!rb)
            {
                rb = Undo.AddComponent<Rigidbody>(_target);
            }
            rb.useGravity = _useGravity;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            EditorUtility.SetDirty(rb);

            // 3. Pi tech Grabbable marker
            var grabbable = _target.GetComponent<Grabbable>();
            if (!grabbable)
            {
                grabbable = Undo.AddComponent<Grabbable>(_target);
            }
            grabbable.kinematicWhileGrabbed = _kinematicWhileGrabbed;
            grabbable.snapBackOnRelease = _snapBack;
            EditorUtility.SetDirty(grabbable);

            // 4. Meta Interaction SDK (reflection)
            if (_addMeta && s_HasMetaInteraction)
                ApplyMetaComponents();

            // 5. Photon Fusion (reflection)
            if (_addFusion && s_HasFusion)
                ApplyFusionComponents();

            Undo.CollapseUndoOperations(group);

            Debug.Log($"[Pi tech] Made '{_target.name}' grabbable." +
                      (_addMeta && s_HasMetaInteraction ? " +Meta" : "") +
                      (_addFusion && s_HasFusion ? " +Fusion" : ""));
        }

        void ApplyMetaComponents()
        {
            // Oculus.Interaction.Grabbable
            if (s_GrabbableType != null && !_target.GetComponent(s_GrabbableType))
                Undo.AddComponent(_target, s_GrabbableType);

            // Oculus.Interaction.GrabInteractable
            if (s_GrabInteractableType != null && !_target.GetComponent(s_GrabInteractableType))
            {
                var grabInteractable = Undo.AddComponent(_target, s_GrabInteractableType);

                // Wire the Grabbable reference if the field exists
                WireField(grabInteractable, "Grabbable", _target.GetComponent(s_GrabbableType));
                WireField(grabInteractable, "_grabbable", _target.GetComponent(s_GrabbableType));
            }

            // HandGrabInteractable (optional — for hand tracking pinch/palm grabs)
            if (_addHandGrab && s_HandGrabInteractableType != null && !_target.GetComponent(s_HandGrabInteractableType))
            {
                var handGrab = Undo.AddComponent(_target, s_HandGrabInteractableType);
                WireField(handGrab, "Grabbable", _target.GetComponent(s_GrabbableType));
                WireField(handGrab, "_grabbable", _target.GetComponent(s_GrabbableType));
            }
        }

        void ApplyFusionComponents()
        {
            // NetworkObject
            if (s_NetworkObjectType != null && !_target.GetComponent(s_NetworkObjectType))
                Undo.AddComponent(_target, s_NetworkObjectType);

            // NetworkRigidbody3D (preferred) or NetworkTransform (fallback)
            if (s_NetworkRigidbody3DType != null && !_target.GetComponent(s_NetworkRigidbody3DType))
            {
                Undo.AddComponent(_target, s_NetworkRigidbody3DType);
            }
            else if (s_NetworkTransformType != null && !_target.GetComponent(s_NetworkTransformType))
            {
                Undo.AddComponent(_target, s_NetworkTransformType);
            }
        }

        // ───────── Helpers ─────────

        static void WireField(Component component, string fieldName, UnityEngine.Object value)
        {
            if (!component || !value) return;

            var type = component.GetType();

            // Try public property
            var prop = type.GetProperty(fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop != null && prop.CanWrite && prop.PropertyType.IsInstanceOfType(value))
            {
                prop.SetValue(component, value);
                EditorUtility.SetDirty(component);
                return;
            }

            // Try serialized field (private with [SerializeField] or public)
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (field != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(component, value);
                EditorUtility.SetDirty(component);
            }
        }

        static void FitColliderToBounds(BoxCollider box)
        {
            var renderers = box.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            // Convert world bounds to local space
            box.center = box.transform.InverseTransformPoint(bounds.center);
            box.size = box.transform.InverseTransformVector(bounds.size);
            box.size = new Vector3(
                Mathf.Abs(box.size.x),
                Mathf.Abs(box.size.y),
                Mathf.Abs(box.size.z));
        }
    }
}
#endif
