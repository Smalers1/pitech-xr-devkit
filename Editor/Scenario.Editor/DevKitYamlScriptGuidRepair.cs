#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Scenario;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// When Unity shows "Missing (Mono Script)", <see cref="DevKitFixMissingScriptRefs.TryAssignScript"/> often cannot run
    /// because <c>m_Component.component</c> is null in the SerializedObject API. Dragging a script onto the slot or adding a
    /// new component creates a <b>new</b> MonoBehaviour instance and drops existing serialized data (including
    /// <see cref="SerializeReference"/> steps). This class rewrites only the <c>m_Script</c> GUID line in the prefab/scene
    /// YAML so the existing component body (same fileID) binds to the correct DevKit script again.
    /// </summary>
    public static class DevKitYamlScriptGuidRepair
    {
        static readonly Regex ScriptRefRegex = new Regex(
            @"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([a-fA-F0-9]{32}),\s*type:\s*3\}",
            RegexOptions.Compiled);

        static readonly Regex ScriptZeroRegex = new Regex(
            @"m_Script:\s*\{fileID:\s*0\}",
            RegexOptions.Compiled);

        /// <summary>SceneManager YAML fields may use 2+ spaces indent depending on nesting / Unity version.</summary>
        static readonly Regex YamlSmScenarioFileId = new Regex(
            @"^\s+scenario\s*:\s*\{fileID:",
            RegexOptions.Multiline | RegexOptions.Compiled);

        static readonly Regex YamlSmAutoStart = new Regex(
            @"^\s+autoStart\s*:",
            RegexOptions.Multiline | RegexOptions.Compiled);

        static readonly Regex YamlSmStatsUi = new Regex(
            @"^\s+statsUI\s*:",
            RegexOptions.Multiline | RegexOptions.Compiled);

        static readonly Regex YamlSmStatsConfig = new Regex(
            @"^\s+statsConfig\s*:",
            RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Resolves <paramref name="assetPath"/> (e.g. <c>Assets/Scenes/Lab.unity</c>) to an absolute path for I/O.
        /// Relative paths depend on the process cwd; using the project root avoids silent no-ops on Windows.
        /// </summary>
        static string ToProjectFilesystemPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            if (Path.IsPathRooted(assetPath))
            {
                return File.Exists(assetPath) ? Path.GetFullPath(assetPath) : null;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
            {
                return null;
            }

            string combined = Path.GetFullPath(
                Path.Combine(projectRoot, assetPath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar)));
            return combined;
        }

        /// <summary>True if <paramref name="assetPath"/> exists on disk (uses project root, not process cwd).</summary>
        public static bool AssetPathExistsOnDisk(string assetPath)
        {
            string fsPath = ToProjectFilesystemPath(assetPath);
            return !string.IsNullOrEmpty(fsPath) && File.Exists(fsPath);
        }

        /// <summary>
        /// Cheap filter before a full YAML parse. Reduces work for unrelated prefabs/scenes.
        /// </summary>
        public static bool MightContainRepairableDevKitBlocks(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("--- !u!114", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            if (text.IndexOf("m_Script: {fileID: 0}", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            bool hasScenarioTitle = text.IndexOf("\n  title:", StringComparison.Ordinal) >= 0 ||
                                    text.IndexOf("\r\n  title:", StringComparison.Ordinal) >= 0;
            if (hasScenarioTitle &&
                (text.IndexOf("steps:", StringComparison.Ordinal) >= 0 ||
                 text.IndexOf("graphNotes:", StringComparison.Ordinal) >= 0))
            {
                return true;
            }

            bool hasSmScenarioRef = text.IndexOf("\n  scenario: {fileID:", StringComparison.Ordinal) >= 0 ||
                                    text.IndexOf("\r\n  scenario: {fileID:", StringComparison.Ordinal) >= 0;
            if (hasSmScenarioRef &&
                (text.IndexOf("autoStart:", StringComparison.Ordinal) >= 0 ||
                 text.IndexOf("statsUI:", StringComparison.Ordinal) >= 0 ||
                 text.IndexOf("statsConfig:", StringComparison.Ordinal) >= 0))
            {
                return true;
            }

            // Looser SceneManager fingerprint (indent / line ending differences vs strict "\n  scenario:" above).
            if (YamlSmScenarioFileId.IsMatch(text) &&
                (YamlSmAutoStart.IsMatch(text) || YamlSmStatsUi.IsMatch(text) || YamlSmStatsConfig.IsMatch(text)))
            {
                return true;
            }

            return false;
        }

        /// <summary>True for lab content under <c>Assets/</c> that can carry Scenario / SceneManager.</summary>
        public static bool IsEligibleUserContentAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string ext = Path.GetExtension(assetPath);
            return ext.Equals(".prefab", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".unity", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Repairs all Scenario / SceneManager MonoBehaviour blocks in one .prefab or .unity asset.</summary>
        /// <returns>Number of m_Script lines updated.</returns>
        public static int RepairPrefabOrSceneAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return 0;
            }

            string ext = Path.GetExtension(assetPath);
            if (!ext.Equals(".prefab", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            string fsPath = ToProjectFilesystemPath(assetPath);
            if (string.IsNullOrEmpty(fsPath) || !File.Exists(fsPath))
            {
                return 0;
            }

            MonoScript scenarioMs = DevKitFixMissingScriptRefs.FindMonoScript(typeof(Scenario));
            MonoScript sceneManagerMs = DevKitFixMissingScriptRefs.FindMonoScript(typeof(SceneManager));
            if (scenarioMs == null || sceneManagerMs == null)
            {
                if (EditorApplication.isCompiling)
                {
                    Debug.LogWarning(
                        "[DevKit] YAML script repair skipped: Scenario/SceneManager MonoScript not resolved while scripts are compiling. " +
                        "It will retry on the next import after compilation finishes.");
                }
                else
                {
                    Debug.LogError(
                        "[DevKit] YAML script repair: could not resolve MonoScript for Scenario or SceneManager. " +
                        "Usually the Pitech.XR.Scenario assembly has compile errors (check Console), the devkit package is incomplete, " +
                        "or you have a duplicate Scenario.cs/SceneManager.cs path that hides the package scripts.");
                }

                return 0;
            }

            string scenarioGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(scenarioMs));
            string sceneManagerGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sceneManagerMs));
            if (string.IsNullOrEmpty(scenarioGuid) || string.IsNullOrEmpty(sceneManagerGuid))
            {
                Debug.LogError("[DevKit] YAML script repair: could not read script GUIDs from AssetDatabase.");
                return 0;
            }

            string text = File.ReadAllText(fsPath);
            if (!MightContainRepairableDevKitBlocks(text))
            {
                return 0;
            }

            string original = text;

            var parts = Regex.Split(text, @"(?=^--- !u!)", RegexOptions.Multiline);
            int fixes = 0;
            var sb = new StringBuilder(text.Length + 64);

            foreach (string part in parts)
            {
                if (part.Length == 0)
                {
                    continue;
                }

                if (!part.StartsWith("--- !u!114", StringComparison.Ordinal))
                {
                    sb.Append(part);
                    continue;
                }

                string updated = TryFixMonoBehaviourSection(part, scenarioGuid, sceneManagerGuid, ref fixes);
                sb.Append(updated);
            }

            if (fixes == 0 || sb.ToString() == original)
            {
                return 0;
            }

            File.WriteAllText(fsPath, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return fixes;
        }

        static string TryFixMonoBehaviourSection(string section, string scenarioGuid, string sceneManagerGuid, ref int fixes)
        {
            string targetGuid = null;
            if (LooksLikeSceneManagerYamlSection(section))
            {
                targetGuid = sceneManagerGuid;
            }
            else if (LooksLikeScenarioYamlSection(section))
            {
                targetGuid = scenarioGuid;
            }

            if (targetGuid == null)
            {
                return section;
            }

            Match m = ScriptRefRegex.Match(section);
            if (m.Success)
            {
                string current = m.Groups[1].Value;
                if (!string.Equals(current, targetGuid, StringComparison.OrdinalIgnoreCase))
                {
                    fixes++;
                    return ScriptRefRegex.Replace(
                        section,
                        $"m_Script: {{fileID: 11500000, guid: {targetGuid}, type: 3}}",
                        1);
                }

                return section;
            }

            if (ScriptZeroRegex.IsMatch(section))
            {
                fixes++;
                return ScriptZeroRegex.Replace(section, $"m_Script: {{fileID: 11500000, guid: {targetGuid}, type: 3}}", 1);
            }

            return section;
        }

        /// <summary>SceneManager serializes a reference field <c>scenario</c> and <c>autoStart</c>; fingerprint is specific enough for lab prefabs.</summary>
        static bool LooksLikeSceneManagerYamlSection(string section)
        {
            if (section.IndexOf("m_Script:", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            if (!YamlSmScenarioFileId.IsMatch(section))
            {
                return false;
            }

            return YamlSmAutoStart.IsMatch(section) ||
                   YamlSmStatsUi.IsMatch(section) ||
                   YamlSmStatsConfig.IsMatch(section);
        }

        /// <summary>Scenario serializes <c>title</c> and usually <c>steps</c> (SerializeReference list may omit <c>steps:</c> when empty).</summary>
        static bool LooksLikeScenarioYamlSection(string section)
        {
            if (section.IndexOf("m_Script:", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            if (LooksLikeSceneManagerYamlSection(section))
            {
                return false;
            }

            bool hasTitle = section.IndexOf("\n  title:", StringComparison.Ordinal) >= 0 ||
                            section.IndexOf("\r\n  title:", StringComparison.Ordinal) >= 0;
            if (!hasTitle)
            {
                return false;
            }

            bool hasSteps = section.IndexOf("\n  steps:", StringComparison.Ordinal) >= 0 ||
                            section.IndexOf("\r\n  steps:", StringComparison.Ordinal) >= 0;
            bool hasGraphNotes = section.IndexOf("graphNotes:", StringComparison.Ordinal) >= 0;
            return hasSteps || hasGraphNotes;
        }

        /// <summary>Collects .prefab / .unity paths backing objects that report missing scripts.</summary>
        public static void CollectBackingAssetPaths(GameObject root, HashSet<string> paths)
        {
            if (root == null || paths == null)
            {
                return;
            }

            CollectBackingAssetPathsRecursive(root, paths);
        }

        static void CollectBackingAssetPathsRecursive(GameObject go, HashSet<string> paths)
        {
            if (go == null)
            {
                return;
            }

            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
            {
                string p = GetBackingAssetPath(go);
                if (!string.IsNullOrEmpty(p))
                {
                    paths.Add(p);
                }
            }

            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                CollectBackingAssetPathsRecursive(t.GetChild(i).gameObject, paths);
            }
        }

        static string GetBackingAssetPath(GameObject go)
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(go);
            if (stage != null && !string.IsNullOrEmpty(stage.assetPath))
            {
                return stage.assetPath;
            }

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                return prefabPath;
            }

            if (go.scene.IsValid() && !string.IsNullOrEmpty(go.scene.path))
            {
                return go.scene.path;
            }

            return null;
        }
    }
}
#endif
