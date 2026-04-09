#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Scenario;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// Re-links <c>m_Script</c> on broken MonoBehaviours without removing the component, so SerializeReference
    /// data (scenario steps) stays on the object. Use when the Inspector shows "Missing (Mono Script)" after
    /// prefab creation, package path changes, or a bad GUID in the prefab YAML.
    /// </summary>
    public static class DevKitFixMissingScriptRefs
    {
        const string MenuPath = "Pi tech/Tools/Fix Missing DevKit Script References on Selection";

        [MenuItem(MenuPath, false, 502)]
        static void FixSelection()
        {
            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            {
                return;
            }

            var yamlPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GameObject root in Selection.gameObjects)
            {
                DevKitYamlScriptGuidRepair.CollectBackingAssetPaths(root, yamlPaths);
            }

            int yamlFixes = 0;
            foreach (string path in yamlPaths)
            {
                yamlFixes += DevKitYamlScriptGuidRepair.RepairPrefabOrSceneAsset(path);
            }

            if (yamlFixes > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            int serializedFixes = 0;
            foreach (GameObject root in Selection.gameObjects)
            {
                serializedFixes += FixRecursive(root);
            }

            int total = yamlFixes + serializedFixes;
            if (total > 0)
            {
                Debug.Log(
                    $"[DevKit] Fixed DevKit script references (YAML line fixes: {yamlFixes}, serialized fixes: {serializedFixes}). " +
                    "YAML repair preserves SerializeReference data; avoid using Remove Component / Add Component for missing scripts.");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Fix missing DevKit scripts",
                    "Nothing was changed.\n\n" +
                    "Typical causes:\n" +
                    "• Console compile errors — fix those first.\n" +
                    "• The broken components are not Scenario / SceneManager (YAML fingerprints did not match).\n" +
                    "• You already removed the missing slot and added a new component — the old serialized data is gone; restore from version control.\n\n" +
                    "Why prefabs \"break\": Unity stores a script file GUID on each component. If that GUID does not match the DevKit copy " +
                    "in this project (package moved, duplicate scripts under Assets, or a bad save while compiling), the Inspector shows " +
                    "Missing Script. Dragging a script onto the slot often creates a new component instance and wipes data; use this menu instead.",
                    "OK");
            }
        }

        [MenuItem("Pi tech/Tools/Repair DevKit script GUIDs in selected prefab/scene asset (YAML only)", false, 503)]
        static void FixSelectedAssetsYamlOnly()
        {
            int yamlFixes = 0;
            foreach (UnityEngine.Object o in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(o);
                yamlFixes += DevKitYamlScriptGuidRepair.RepairPrefabOrSceneAsset(path);
            }

            if (yamlFixes > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[DevKit] YAML script GUID repair: {yamlFixes} m_Script line(s) updated on selected asset(s).");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "YAML script repair",
                    "Select one or more .prefab or .unity assets in the Project window, then run this again.",
                    "OK");
            }
        }

        [MenuItem("Pi tech/Tools/Repair DevKit script GUIDs in selected prefab/scene asset (YAML only)", true)]
        static bool ValidateFixSelectedAssetsYamlOnly()
        {
            if (Selection.objects == null || Selection.objects.Length == 0)
            {
                return false;
            }

            foreach (UnityEngine.Object o in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string lower = path.ToLowerInvariant();
                if (lower.EndsWith(".prefab", StringComparison.Ordinal) || lower.EndsWith(".unity", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateFixSelection()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        static int FixRecursive(GameObject go)
        {
            if (go == null)
            {
                return 0;
            }

            int n = TryFixGameObject(go);
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                n += FixRecursive(t.GetChild(i).gameObject);
            }

            return n;
        }

        static int TryFixGameObject(GameObject go)
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missing <= 0)
            {
                return 0;
            }

            Type guess = GuessTypeFromObjectName(go.name);
            if (missing == 1 && guess != null)
            {
                return TryAssignScript(go, guess) ? 1 : 0;
            }

            if (missing == 1 && guess == null)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Fix missing script",
                    $"GameObject '{go.name}' has one missing script. Which DevKit type should be assigned?",
                    "Scenario",
                    "Cancel",
                    "Scene Manager");
                if (choice == 0)
                {
                    return TryAssignScript(go, typeof(Scenario)) ? 1 : 0;
                }

                if (choice == 2)
                {
                    return TryAssignScript(go, typeof(SceneManager)) ? 1 : 0;
                }

                return 0;
            }

            EditorUtility.DisplayDialog(
                "Fix missing scripts",
                $"'{go.name}' has {missing} missing script slots on one GameObject. " +
                "Fix each component manually (remove slot + add component loses step data), or split into separate objects. " +
                "This command only auto-fixes objects with exactly one missing script.",
                "OK");
            return 0;
        }

        static Type GuessTypeFromObjectName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            string n = objectName.Trim();
            if (n.IndexOf("scene manager", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("scenemanager", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return typeof(SceneManager);
            }

            if (n.IndexOf("scenario", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return typeof(Scenario);
            }

            return null;
        }

        static bool ScriptPropertyNeedsFix(SerializedProperty mScript)
        {
            if (mScript == null)
            {
                return false;
            }

            var r = mScript.objectReferenceValue;
            if (r == null)
            {
                return true;
            }

            if (r is MonoScript scr && scr.GetClass() == null)
            {
                return true;
            }

            return false;
        }

        static bool TryAssignScript(GameObject go, Type componentType)
        {
            MonoScript scriptAsset = FindMonoScript(componentType);
            if (scriptAsset == null)
            {
                Debug.LogError(
                    $"[DevKit] Could not find MonoScript for {componentType.FullName}. Is com.pitech.xr.devkit installed and compiling?",
                    go);
                return false;
            }

            SerializedObject soGo = new SerializedObject(go);
            SerializedProperty comps = soGo.FindProperty("m_Component");
            if (comps == null)
            {
                return false;
            }

            Undo.RecordObject(go, "Fix missing DevKit script reference");

            bool any = false;
            for (int i = 0; i < comps.arraySize; i++)
            {
                SerializedProperty pair = comps.GetArrayElementAtIndex(i);
                SerializedProperty cref = pair.FindPropertyRelative("component");
                var c = cref.objectReferenceValue as Component;
                if (c == null)
                {
                    continue;
                }

                SerializedObject soC = new SerializedObject(c);
                SerializedProperty mScript = soC.FindProperty("m_Script");
                if (!ScriptPropertyNeedsFix(mScript))
                {
                    continue;
                }

                Undo.RecordObject(c, "Fix missing DevKit script reference");
                mScript.objectReferenceValue = scriptAsset;
                soC.ApplyModifiedProperties();
                any = true;
                break;
            }

            soGo.ApplyModifiedProperties();
            if (any)
            {
                EditorUtility.SetDirty(go);
            }

            return any;
        }

        internal static MonoScript FindMonoScript(Type behaviourType)
        {
            if (behaviourType == null)
            {
                return null;
            }

            MonoScript byDb = FindMonoScriptViaAssetDatabase(behaviourType);
            if (byDb != null)
            {
                return byDb;
            }

            MonoScript byKnownPath = FindMonoScriptByDevKitSourcePath(behaviourType);
            if (byKnownPath != null)
            {
                return byKnownPath;
            }

            var scripts = Resources.FindObjectsOfTypeAll<MonoScript>();
            for (int i = 0; i < scripts.Length; i++)
            {
                var s = scripts[i];
                if (s == null)
                {
                    continue;
                }

                if (s.GetClass() == behaviourType)
                {
                    return s;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves <see cref="MonoScript"/> by compiled type. Fails when <see cref="MonoScript.GetClass"/> is null
        /// (compile errors or script reload window).
        /// </summary>
        static MonoScript FindMonoScriptViaAssetDatabase(Type behaviourType)
        {
            string[] guids = AssetDatabase.FindAssets("t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                MonoScript ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms == null)
                {
                    continue;
                }

                if (ms.GetClass() == behaviourType)
                {
                    return ms;
                }
            }

            return null;
        }

        /// <summary>
        /// When runtime scripts do not compile, <see cref="MonoScript.GetClass"/> is null but the asset (and .meta GUID)
        /// still exist. YAML repair only needs the script file GUID, so match known DevKit paths under any package root.
        /// </summary>
        static MonoScript FindMonoScriptByDevKitSourcePath(Type behaviourType)
        {
            string pathSuffix = null;
            if (behaviourType == typeof(Scenario))
            {
                pathSuffix = "Scenario/Scenario.cs";
            }
            else if (behaviourType == typeof(SceneManager))
            {
                pathSuffix = "Scenario/SceneManager.cs";
            }
            else
            {
                return null;
            }

            string[] guids = AssetDatabase.FindAssets("t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string normalized = path.Replace('\\', '/');
                if (!normalized.EndsWith(pathSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            }

            return null;
        }
    }
}
#endif
