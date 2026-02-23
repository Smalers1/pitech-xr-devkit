using System;
using Pitech.XR.Core;
using UnityEngine;

namespace Pitech.XR.ContentDelivery
{
    public interface ILaunchContextProvider
    {
        bool TryBuildLaunchContext(AddressablesModuleConfig config, out LaunchContext context);
    }

    public interface IContentDeliveryService : IXRService
    {
        bool IsReady { get; }
        LaunchContext CurrentContext { get; }
        event Action<LaunchContext> OnLaunchContextResolved;
        void SetLaunchContext(LaunchContext context);
        bool TryGetCurrentContext(out LaunchContext context);
        bool TryReconcileAttempt(string launchRequestId, string canonicalAttemptId);
    }

    public sealed class ContentDeliveryRuntimeService : IContentDeliveryService
    {
        private readonly AddressablesModuleConfig config;
        private LaunchContext currentContext;
        private bool isReady;

        public ContentDeliveryRuntimeService(AddressablesModuleConfig configAsset)
        {
            config = configAsset;
        }

        public bool IsReady => isReady;
        public LaunchContext CurrentContext => currentContext;

        public event Action<LaunchContext> OnLaunchContextResolved;

        public void Initialize()
        {
            isReady = true;
        }

        public void Shutdown()
        {
            isReady = false;
            currentContext = null;
        }

        public void SetLaunchContext(LaunchContext context)
        {
            if (context == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(context.requestedAt))
            {
                context.requestedAt = Timestamp.UtcNowIso8601();
            }

            bool cfgOffline = config == null || config.allowOfflineCacheLaunch;
            bool cfgOlderCache = config == null || config.allowOlderCachedSameLab;
            bool cfgNetworkMiss = config == null || config.networkRequiredIfCacheMiss;
            context.allowOfflineCacheLaunch = context.allowOfflineCacheLaunch || cfgOffline;
            context.allowOlderCachedSameLab = context.allowOlderCachedSameLab || cfgOlderCache;
            context.networkRequiredIfCacheMiss = context.networkRequiredIfCacheMiss || cfgNetworkMiss;

            if (!LaunchContextValidation.TryValidateRuntimeLaunchContext(context, out string validationError))
            {
                Debug.LogError($"[ContentDelivery] Launch context rejected: {validationError}");
                return;
            }

            currentContext = context;
            OnLaunchContextResolved?.Invoke(context);
        }

        public bool TryGetCurrentContext(out LaunchContext context)
        {
            context = currentContext;
            return context != null;
        }

        public bool TryReconcileAttempt(string launchRequestId, string canonicalAttemptId)
        {
            return AttemptIdentityManager.TryReconcile(launchRequestId, canonicalAttemptId);
        }
    }

    /// <summary>
    /// Entry-point for external bridge systems to hand off launch payloads.
    /// </summary>
    public static class LaunchContextRegistry
    {
        private static readonly object Sync = new object();
        private static LaunchContext pendingExternal;

        public static void SetExternalContext(LaunchContext context)
        {
            if (context == null)
            {
                return;
            }

            lock (Sync)
            {
                context.source = LaunchSource.ReactNativeBridge;
                pendingExternal = context;
            }
        }

        public static bool TryConsumeExternalContext(out LaunchContext context)
        {
            lock (Sync)
            {
                context = pendingExternal;
                pendingExternal = null;
                return context != null;
            }
        }
    }
}
