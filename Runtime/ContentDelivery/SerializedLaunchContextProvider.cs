using UnityEngine;

namespace Pitech.XR.ContentDelivery
{
    /// <summary>
    /// Simple provider for Unity-only menu launch scenarios.
    /// </summary>
    [AddComponentMenu("Pi tech XR/Content Delivery/Serialized Launch Context Provider")]
    public sealed class SerializedLaunchContextProvider : MonoBehaviour, ILaunchContextProvider
    {
        public LaunchSource source = LaunchSource.UnityMenu;
        public string labId = "default";
        public string addressKey = string.Empty;
        public string resolvedVersionId = string.Empty;
        public string runtimeUrl = string.Empty;

        public bool TryBuildLaunchContext(AddressablesModuleConfig config, out LaunchContext context)
        {
            if (source == LaunchSource.ReactNativeBridge)
            {
                context = null;
                return false;
            }

            context = source == LaunchSource.UnityMenu
                ? LaunchContextFactory.CreateUnityMenuContext(labId, resolvedVersionId, runtimeUrl, config)
                : LaunchContextFactory.CreateDirectContext(config);
            context.source = source;
            if (!string.IsNullOrWhiteSpace(addressKey))
            {
                context.addressKey = addressKey.Trim();
            }
            return true;
        }
    }
}
