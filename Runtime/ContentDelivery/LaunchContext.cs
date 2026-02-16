using System;

namespace Pitech.XR.ContentDelivery
{
    public enum LaunchSource
    {
        ReactNativeBridge = 0,
        UnityMenu = 1,
        Direct = 2,
    }

    [Serializable]
    public sealed class LaunchContext
    {
        public string contractVersion = "1.1.0";
        public string launchRequestId = string.Empty;
        public string attemptId = string.Empty;
        public string idempotencyKey = string.Empty;
        public string labId = string.Empty;
        public string addressKey = string.Empty;
        public string resolvedVersionId = string.Empty;
        public string runtimeUrl = string.Empty;
        public bool launchedFromCache;
        public bool allowOfflineCacheLaunch;
        public bool allowOlderCachedSameLab;
        public bool networkRequiredIfCacheMiss;
        public LaunchSource source = LaunchSource.Direct;
        public string requestedAt = string.Empty;
    }

    public static class LaunchContextFactory
    {
        public static LaunchContext CreateUnityMenuContext(
            string labId,
            string resolvedVersionId,
            string runtimeUrl,
            AddressablesModuleConfig config)
        {
            AttemptIdentity identity = AttemptIdentityManager.CreateLocalFirst(labId);
            return new LaunchContext
            {
                launchRequestId = identity.launchRequestId,
                attemptId = identity.attemptId,
                idempotencyKey = identity.idempotencyKey,
                labId = Safe(labId),
                resolvedVersionId = Safe(resolvedVersionId),
                runtimeUrl = Safe(runtimeUrl),
                source = LaunchSource.UnityMenu,
                requestedAt = Timestamp.UtcNowIso8601(),
                allowOfflineCacheLaunch = config == null || config.allowOfflineCacheLaunch,
                allowOlderCachedSameLab = config == null || config.allowOlderCachedSameLab,
                networkRequiredIfCacheMiss = config == null || config.networkRequiredIfCacheMiss,
            };
        }

        public static LaunchContext CreateDirectContext(AddressablesModuleConfig config)
        {
            AttemptIdentity identity = AttemptIdentityManager.CreateLocalFirst("direct");
            return new LaunchContext
            {
                launchRequestId = identity.launchRequestId,
                attemptId = identity.attemptId,
                idempotencyKey = identity.idempotencyKey,
                labId = "direct",
                source = LaunchSource.Direct,
                requestedAt = Timestamp.UtcNowIso8601(),
                allowOfflineCacheLaunch = config == null || config.allowOfflineCacheLaunch,
                allowOlderCachedSameLab = config == null || config.allowOlderCachedSameLab,
                networkRequiredIfCacheMiss = config == null || config.networkRequiredIfCacheMiss,
            };
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
