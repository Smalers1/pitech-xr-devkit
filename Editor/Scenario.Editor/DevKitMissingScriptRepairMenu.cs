#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Scenario;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// When a prefab shows "Missing (Mono Script)" on Scenario or SceneManager, Unity lost the
    /// link to the script GUID (package update, embed vs PackageCache, or saving prefab while scripts failed to compile).
    /// This menu removes the broken component slot and adds a fresh DevKit component. Serialized data on that
    /// slot is lost — use version control / backups for step graphs when possible.
    /// </summary>
    public static class DevKitMissingScriptRepairMenu
    {
        const string MenuRoot = "Pi tech/Tools/Repair Missing DevKit Scripts";

        [MenuItem(MenuRoot + "/Reattach Scenario or Scene Manager on Selection (heuristic)", false, 500)]
        static void RepairSelectionHeuristic()
        {
            if (!TryConfirmDataLoss())
            {
                return;
            }

            foreach (GameObject go in Selection.gameObjects)
            {
                RepairGameObjectRecursive(go);
            }
        }

        [MenuItem(MenuRoot + "/Reattach Scenario or Scene Manager on Selection (heuristic)", true)]
        static bool ValidateRepairSelection()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        static bool TryConfirmDataLoss()
        {
            return EditorUtility.DisplayDialog(
                "Repair missing DevKit scripts",
                "This removes Missing Script component slots and adds a new Scenario or SceneManager. " +
                "Any serialized data that lived on the missing component (including scenario steps) cannot be recovered by this tool.\n\n" +
                "Prefer restoring from Git or re-assigning the script manually if Unity still shows a script field.\n\n" +
                "Continue?",
                "Continue",
                "Cancel");
        }

        static void RepairGameObjectRecursive(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            RepairSingleGameObject(root);
            Transform t = root.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                RepairGameObjectRecursive(t.GetChild(i).gameObject);
            }
        }

        static void RepairSingleGameObject(GameObject go)
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missing <= 0)
            {
                return;
            }

            if (missing > 1)
            {
                if (EditorUtility.DisplayDialog(
                        "Multiple missing scripts",
                        $"'{go.name}' has {missing} missing script slots. Unity can strip all of them at once, but this tool will not guess which components to re-add.\n\n" +
                        "Strip all missing scripts on this object now?",
                        "Strip all",
                        "Skip"))
                {
                    Undo.RegisterCompleteObjectUndo(go, "Remove missing scripts");
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    EditorUtility.SetDirty(go);
                    Debug.LogWarning(
                        $"[DevKit] Removed {missing} missing script(s) from '{go.name}'. Add Scenario / Scene Manager / other components manually.",
                        go);
                }

                return;
            }

            Type attachType = GuessDevKitType(go.name);
            if (attachType == null)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Repair missing DevKit script",
                    $"GameObject '{go.name}' has one missing script. Which component should be added?",
                    "Scenario",
                    "Cancel",
                    "Scene Manager");
                if (choice == 0)
                {
                    attachType = typeof(Scenario);
                }
                else if (choice == 2)
                {
                    attachType = typeof(SceneManager);
                }
                else
                {
                    return;
                }
            }

            Undo.RegisterCompleteObjectUndo(go, "Repair missing DevKit script");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

            if (attachType == typeof(Scenario))
            {
                if (go.GetComponent<Scenario>() == null)
                {
                    Undo.AddComponent<Scenario>(go);
                    Debug.Log($"[DevKit] Added Scenario to '{go.name}'. Rebuild steps in the Scenario graph if needed.", go);
                }
                else
                {
                    Debug.Log(
                        $"[DevKit] Removed missing script on '{go.name}'; Scenario was already present. Add Scene Manager or other components manually if needed.",
                        go);
                }
            }
            else if (attachType == typeof(SceneManager))
            {
                if (go.GetComponent<SceneManager>() == null)
                {
                    Undo.AddComponent<SceneManager>(go);
                    Debug.Log($"[DevKit] Added SceneManager to '{go.name}'. Re-wire references on the prefab.", go);
                }
                else
                {
                    Debug.Log(
                        $"[DevKit] Removed missing script on '{go.name}'; SceneManager was already present. Add Scenario or other components manually if needed.",
                        go);
                }
            }

            EditorUtility.SetDirty(go);
        }

        static Type GuessDevKitType(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            string n = objectName.Trim();
            if (n.IndexOf("scenario", StringComparison.OrdinalIgnoreCase) >= 0 &&
                n.IndexOf("scene manager", StringComparison.OrdinalIgnoreCase) < 0 &&
                n.IndexOf("scenemanager", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return typeof(Scenario);
            }

            if (n.IndexOf("scene manager", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("scenemanager", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return typeof(SceneManager);
            }

            return null;
        }
    }
}
#endif
