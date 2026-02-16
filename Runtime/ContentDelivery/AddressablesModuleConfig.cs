using UnityEngine;

namespace Pitech.XR.ContentDelivery
{
    public enum ContentDeliveryProvider
    {
        None = 0,
        CCD = 1,
        Custom = 2,
    }

    public enum ContentDeliveryEnvironment
    {
        Development = 0,
        Staging = 1,
        Production = 2,
    }

    public enum CatalogMode
    {
        Local = 0,
        Remote = 1,
    }

    /// <summary>
    /// Minimal, generic content-delivery configuration.
    /// Project-specific behavior is resolved via adapterTypeName.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Pi tech/Content Delivery/Addressables Module Config",
        fileName = "AddressablesModuleConfig")]
    public sealed class AddressablesModuleConfig : ScriptableObject
    {
        [Header("Core")]
        public ContentDeliveryProvider provider = ContentDeliveryProvider.CCD;
        public ContentDeliveryEnvironment environment = ContentDeliveryEnvironment.Development;
        public CatalogMode catalogMode = CatalogMode.Remote;

        [Header("Remote Catalog")]
        [Tooltip("Optional base URL for remote catalog content.")]
        public string remoteCatalogBaseUrl = string.Empty;

        [Tooltip("Template supports {baseUrl}, {environment}, {labId}, {resolvedVersionId}.")]
        public string remoteCatalogUrlTemplate =
            "{baseUrl}/{environment}/{labId}/{resolvedVersionId}/catalog.json";

        [Header("Conventions")]
        [Tooltip("Template supports {labId}. Used when creating remote group names.")]
        public string groupNameTemplate = "lab_{labId}";

        [Tooltip("Addressables profile used for build/validation.")]
        public string profileName = "Default";

        [Tooltip("Optional assembly-qualified adapter type for project-specific conventions.")]
        public string adapterTypeName = string.Empty;

        [Header("Runtime Policy")]
        public bool allowOfflineCacheLaunch = true;
        public bool allowOlderCachedSameLab = true;
        public bool networkRequiredIfCacheMiss = true;

        [Header("Local Output")]
        [Tooltip("Project-relative root for local output (Addressables bundles + report JSON). Recommended outside Assets.")]
        public string localWorkspaceRoot = "Build/ContentDelivery";

        [Tooltip("Optional project-relative JSON report folder. If empty, uses {localWorkspaceRoot}/Reports.")]
        public string localReportsFolder = string.Empty;

        [Header("Advanced")]
        [Tooltip("Enables internal/test-only Build action in Guided Setup.")]
        public bool enableHiddenBuildAction;
    }
}
