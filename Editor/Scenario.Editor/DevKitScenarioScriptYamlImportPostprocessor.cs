#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// Root-cause mitigation: Unity sometimes writes a prefab/scene with a <c>m_Script</c> GUID that does not resolve
    /// (package refresh, domain reload edge cases, or stale GUIDs). That surfaces as "Missing (Mono Script)" on
    /// <see cref="Scenario"/> / <see cref="SceneManager"/> even though the rest of the YAML (including SerializeReference
    /// steps) is still present. We normalize those two component types on import so labs under <c>Assets/</c> self-heal
    /// without a manual repair step.
    /// </summary>
    public sealed class DevKitScenarioScriptYamlImportPostprocessor : AssetPostprocessor
    {
        static readonly HashSet<string> PendingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static bool s_FlushScheduled;

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (EditorApplication.isCompiling)
            {
                return;
            }

            Enqueue(importedAssets);
            Enqueue(movedAssets);
            ScheduleFlush();
        }

        static void Enqueue(string[] paths)
        {
            if (paths == null)
            {
                return;
            }

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (DevKitYamlScriptGuidRepair.IsEligibleUserContentAssetPath(path))
                {
                    PendingPaths.Add(path);
                }
            }
        }

        static void ScheduleFlush()
        {
            if (s_FlushScheduled)
            {
                return;
            }

            s_FlushScheduled = true;
            EditorApplication.delayCall += FlushOnce;
        }

        static void FlushOnce()
        {
            EditorApplication.delayCall -= FlushOnce;
            s_FlushScheduled = false;

            if (EditorApplication.isCompiling)
            {
                ScheduleFlush();
                return;
            }

            try
            {
                while (PendingPaths.Count > 0)
                {
                    var batch = new List<string>(PendingPaths);
                    PendingPaths.Clear();

                    for (int i = 0; i < batch.Count; i++)
                    {
                        string path = batch[i];
                        if (!DevKitYamlScriptGuidRepair.IsEligibleUserContentAssetPath(path) || !File.Exists(path))
                        {
                            continue;
                        }

                        try
                        {
                            DevKitYamlScriptGuidRepair.RepairPrefabOrSceneAsset(path);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[DevKit] Automatic script GUID repair skipped for '{path}': {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DevKit] Automatic script GUID repair batch failed: {ex.Message}");
            }
        }
    }
}
#endif
