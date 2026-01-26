#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Core.Editor
{
    /// <summary>
    /// A "Dev Block" is a curated prefab entry that can be browsed and instantiated into scenes.
    /// Lives as a ScriptableObject asset in the project (recommended default path: Assets/Pi tech/Dev Blocks).
    /// </summary>
    [CreateAssetMenu(menuName = "Pi tech/Dev Blocks/Dev Block", fileName = "DevBlock")]
    public sealed class DevBlockItem : ScriptableObject
    {
        [Header("Core")]
        public GameObject prefab;

        [Tooltip("Display name shown in the Dev Blocks window. If empty, prefab name is used.")]
        public string displayName;

        [Tooltip("Scene anchor under which the prefab will be instantiated (e.g. --- INTERACTABLES ---).")]
        public string category = "--- INTERACTABLES ---";

        [Header("Optional metadata")]
        [TextArea(2, 6)]
        public string description;

        public List<string> tags = new();

        [Header("Suggested dependencies (prompted on Add)")]
        [Tooltip("If these component types are missing in the active scene, Dev Blocks will suggest creating them under --- SCENE MANAGERS ---.\nUse full type names, e.g. 'Pitech.XR.Interactables.SelectablesManager'.")]
        public List<string> suggestedManagerComponentTypes = new();

        public string EffectiveName
            => !string.IsNullOrWhiteSpace(displayName)
                ? displayName.Trim()
                : (prefab != null ? prefab.name : name);

        public bool HasPrefab => prefab != null;

        public IEnumerable<string> TagsSafe
            => tags == null ? Array.Empty<string>() : tags;
    }
}
#endif


