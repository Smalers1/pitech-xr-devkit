#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace Pitech.XR.Interactables.Editor
{
    /// <summary>
    /// Right-click context menu "Pi tech > Make Grabbable".
    /// Adds the correct parent components (Collider, Rigidbody, Meta Grabbable,
    /// Fusion networking, multiplayer shared-grab bridge) then invokes Meta's own
    /// Grab Wizard ("Add Grab Interaction") to create the ISDK_HandGrabInteraction
    /// child with proper interactors.
    ///
    /// Everything is reflection-based — the DevKit compiles without Meta or Fusion.
    /// </summary>
    public static class MakeGrabbableWizard
    {
        [MenuItem("GameObject/Pi tech/Make Grabbable", false, 10)]
        static void MakeGrabbable(MenuCommand cmd)
        {
            var go = cmd.context as GameObject;
            if (!go) return;

            if (Selection.gameObjects.Length > 1 && cmd.context != Selection.activeGameObject)
                return;

            MakeGrabbableWindow.Open(go);
        }

        [MenuItem("GameObject/Pi tech/Make Grabbable", true)]
        static bool MakeGrabbableValidate() => Selection.activeGameObject != null;
    }

    public class MakeGrabbableWindow : EditorWindow
    {
        // ───────── SDK Detection (cached) ─────────
        static bool s_Probed;
        static bool s_HasMetaInteraction;
        static bool s_HasFusion;

        // Meta Interaction SDK — parent-level only
        static Type s_MetaGrabbableType;          // Oculus.Interaction.Grabbable

        // Meta + Fusion multiplayer bridge
        static Type s_SharedGrabbableType;        // Meta XR Shared Grabbable
        static Type s_TransferOwnershipType;      // Transfer Ownership Fusion

        // Photon Fusion
        static Type s_NetworkObjectType;          // Fusion.NetworkObject
        static Type s_NetworkTransformType;       // Fusion.NetworkTransform

        // Meta Grab Wizard menu path
        const string META_GRAB_WIZARD_MENU = "GameObject/Interaction SDK/Add Grab Interaction";

        static void ProbeSDKs()
        {
            if (s_Probed) return;
            s_Probed = true;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Meta Interaction SDK — only the parent Grabbable
            s_MetaGrabbableType = FindType(assemblies, "Oculus.Interaction.Grabbable");
            s_HasMetaInteraction = s_MetaGrabbableType != null;

            // Meta + Fusion multiplayer bridge components
            // These live in Meta's multiplayer building blocks
            s_SharedGrabbableType = FindType(assemblies, "Meta.XR.MultiplayerBlocks.Shared.SharedGrabbable")
                                 ?? FindTypeByName(assemblies, "MetaXRSharedGrabbable")
                                 ?? FindTypeByName(assemblies, "SharedGrabbable");
            s_TransferOwnershipType = FindTypeByName(assemblies, "TransferOwnershipFusion");

            // Photon Fusion
            s_NetworkObjectType = FindType(assemblies, "Fusion.NetworkObject");
            s_NetworkTransformType = FindType(assemblies, "Fusion.NetworkTransform");
            s_HasFusion = s_NetworkObjectType != null;
        }

        static Type FindType(Assembly[] assemblies, string fullName)
        {
            foreach (var asm in assemblies)
            {
                var t = asm.GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Fallback: find by short class name across all assemblies.</summary>
        static Type FindTypeByName(Assembly[] assemblies, string shortName)
        {
            foreach (var asm in assemblies)
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == shortName && !t.IsAbstract)
                            return t;
                    }
                }
                catch { /* ReflectionTypeLoadException — skip */ }
            }
            return null;
        }

        // ───────── Instance State ─────────
        GameObject _target;
        bool _addMeta = true;
        bool _addFusion;
        bool _useGravity = true;
        bool _kinematicWhileGrabbed = true;
        bool _snapBack;
        bool _addColliderIfMissing = true;

        public static void Open(GameObject target)
        {
            ProbeSDKs();
            var win = GetWindow<MakeGrabbableWindow>(utility: true, "Make Grabbable");
            win._target = target;
            win._addMeta = s_HasMetaInteraction;
            win._addFusion = s_HasFusion; // default on when available
            win.minSize = new Vector2(380, 300);
            win.maxSize = new Vector2(420, 480);
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
                    _addMeta = EditorGUILayout.Toggle("Add Meta Grabbable + Open Grab Wizard", _addMeta);
                    if (_addMeta)
                    {
                        EditorGUILayout.HelpBox(
                            "Adds Meta's Grabbable on the parent, then opens Meta's\n" +
                            "Grab Wizard to create the ISDK_HandGrabInteraction child.\n" +
                            "Click \"Create\" in that wizard to finish.",
                            MessageType.None);
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
                        string fusionInfo = "Adds: NetworkObject, NetworkTransform";
                        if (s_HasMetaInteraction && _addMeta)
                        {
                            if (s_SharedGrabbableType != null)
                                fusionInfo += ", Meta XR Shared Grabbable";
                            if (s_TransferOwnershipType != null)
                                fusionInfo += ", Transfer Ownership Fusion";
                        }
                        EditorGUILayout.HelpBox(fusionInfo, MessageType.None);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Photon Fusion not detected.\n" +
                        "Install Photon Fusion to enable multiplayer components.",
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

            // 1. Collider (auto-fit BoxCollider if none exists)
            if (_addColliderIfMissing && !_target.GetComponent<Collider>())
            {
                var box = Undo.AddComponent<BoxCollider>(_target);
                FitColliderToBounds(box);
            }

            // 2. Rigidbody
            var rb = _target.GetComponent<Rigidbody>();
            if (!rb)
                rb = Undo.AddComponent<Rigidbody>(_target);
            rb.useGravity = _useGravity;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            EditorUtility.SetDirty(rb);

            // 3. Meta Grabbable on the PARENT (just the Grabbable — not interactables)
            if (_addMeta && s_HasMetaInteraction)
                AddIfMissing(_target, s_MetaGrabbableType);

            // 5. Fusion networking
            if (_addFusion && s_HasFusion)
            {
                AddIfMissing(_target, s_NetworkObjectType);
                AddIfMissing(_target, s_NetworkTransformType);

                // Multiplayer grab bridge (only when BOTH Meta + Fusion are present)
                if (_addMeta && s_HasMetaInteraction)
                {
                    AddIfMissing(_target, s_SharedGrabbableType);
                    AddIfMissing(_target, s_TransferOwnershipType);
                }
            }

            Undo.CollapseUndoOperations(group);

            string summary = $"[Pi tech] Made '{_target.name}' grabbable.";
            if (_addMeta && s_HasMetaInteraction) summary += " +Meta Grabbable";
            if (_addFusion && s_HasFusion) summary += " +Fusion";
            Debug.Log(summary);

            // 6. Open Meta's Grab Wizard to create the ISDK_HandGrabInteraction child
            //    This must happen AFTER our window closes so Selection is correct.
            if (_addMeta && s_HasMetaInteraction)
            {
                // Ensure the target is selected so Meta's wizard targets it
                Selection.activeGameObject = _target;

                // Defer one frame so our utility window is fully closed first
                EditorApplication.delayCall += () =>
                {
                    Selection.activeGameObject = _target;
                    bool executed = EditorApplication.ExecuteMenuItem(META_GRAB_WIZARD_MENU);
                    if (executed)
                    {
                        Debug.Log("[Pi tech] Opened Meta's Grab Wizard — click \"Create\" to add ISDK_HandGrabInteraction.");
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[Pi tech] Could not find menu \"{META_GRAB_WIZARD_MENU}\".\n" +
                            "You may need to manually right-click > Interaction SDK > Add Grab Interaction.");
                    }
                };
            }
        }

        // ───────── Helpers ─────────

        static void AddIfMissing(GameObject go, Type type)
        {
            if (type == null) return;
            if (go.GetComponent(type)) return;
            Undo.AddComponent(go, type);
        }

        static void FitColliderToBounds(BoxCollider box)
        {
            var renderers = box.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

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
