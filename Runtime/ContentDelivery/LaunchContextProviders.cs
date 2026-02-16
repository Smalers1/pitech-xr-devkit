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

    /// <summary>
    /// Optional receiver for RN/UaaL bridge payload handoff.
    /// Native bridge code can call ReceiveLaunchContextJson.
    /// </summary>
    [AddComponentMenu("Pi tech XR/Content Delivery/Bridge Launch Context Receiver")]
    public sealed class BridgeLaunchContextReceiver : MonoBehaviour
    {
        public void ReceiveLaunchContextJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            LaunchContext context = JsonUtility.FromJson<LaunchContext>(json);
            if (context == null)
            {
                return;
            }

            context.source = LaunchSource.ReactNativeBridge;
            if (string.IsNullOrWhiteSpace(context.requestedAt))
            {
                context.requestedAt = Timestamp.UtcNowIso8601();
            }

            // Ensure attempt lineage exists even when payload is partial.
            if (string.IsNullOrWhiteSpace(context.launchRequestId) ||
                string.IsNullOrWhiteSpace(context.attemptId) ||
                string.IsNullOrWhiteSpace(context.idempotencyKey))
            {
                AttemptIdentity local = AttemptIdentityManager.CreateLocalFirst(context.labId);
                if (string.IsNullOrWhiteSpace(context.launchRequestId))
                {
                    context.launchRequestId = local.launchRequestId;
                }
                if (string.IsNullOrWhiteSpace(context.attemptId))
                {
                    context.attemptId = local.attemptId;
                }
                if (string.IsNullOrWhiteSpace(context.idempotencyKey))
                {
                    context.idempotencyKey = local.idempotencyKey;
                }
            }

            LaunchContextRegistry.SetExternalContext(context);
        }
    }
}
