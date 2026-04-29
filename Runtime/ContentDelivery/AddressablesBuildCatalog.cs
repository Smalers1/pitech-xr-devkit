using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.ContentDelivery
{
    /// <summary>
    /// Saved authoring preset for Addressables Builder (lab id, version, CCD bucket id, asset).
    /// Either <see cref="prefab"/> (AR / UaaL pattern) or <see cref="sceneAsset"/> (VR Shell pattern)
    /// is set per preset; the Builder picks whichever is non-null, scene wins if both are set.
    /// </summary>
    [Serializable]
    public sealed class AddressablesBuildPreset
    {
        public string displayName = "New preset";
        public string labId = "default";
        public string labVersionId = string.Empty;
        public string ccdBucketId = string.Empty;
        public GameObject prefab;
#if UNITY_EDITOR
        public UnityEditor.SceneAsset sceneAsset;
#endif
    }

    /// <summary>
    /// Optional catalog of build presets for the Addressables Builder dropdown.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Pi tech/Content Delivery/Addressables Build Catalog",
        fileName = "AddressablesBuildCatalog")]
    public sealed class AddressablesBuildCatalog : ScriptableObject
    {
        public List<AddressablesBuildPreset> presets = new List<AddressablesBuildPreset>();
    }
}
