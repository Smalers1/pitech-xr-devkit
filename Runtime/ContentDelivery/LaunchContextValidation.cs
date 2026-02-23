using System;

namespace Pitech.XR.ContentDelivery
{
    internal static class LaunchContextValidation
    {
        public static bool TryValidateLineage(
            LaunchContext context,
            bool requireResolvedVersionId,
            out string reason)
        {
            reason = string.Empty;
            if (context == null)
            {
                reason = "missing launch context";
                return false;
            }

            context.launchRequestId = Safe(context.launchRequestId);
            context.attemptId = Safe(context.attemptId);
            context.idempotencyKey = Safe(context.idempotencyKey);
            context.labId = Safe(context.labId);
            context.resolvedVersionId = Safe(context.resolvedVersionId);
            context.runtimeUrl = Safe(context.runtimeUrl);
            context.requestedAt = Safe(context.requestedAt);

            if (string.IsNullOrWhiteSpace(context.launchRequestId))
            {
                reason = "launchRequestId is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(context.attemptId))
            {
                reason = "attempt_id is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(context.idempotencyKey))
            {
                reason = "idempotency_key is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(context.labId))
            {
                reason = "lab_id is required";
                return false;
            }

            if (requireResolvedVersionId && string.IsNullOrWhiteSpace(context.resolvedVersionId))
            {
                reason = "resolvedVersionId is required";
                return false;
            }

            return true;
        }

        public static bool TryValidateRuntimeLaunchContext(LaunchContext context, out string reason)
        {
            if (!TryValidateLineage(context, requireResolvedVersionId: false, out reason))
            {
                return false;
            }

            bool onlineLaunch = IsExternalOnlineLaunch(context);
            if (onlineLaunch && string.IsNullOrWhiteSpace(context.resolvedVersionId))
            {
                reason = "resolvedVersionId is required for online launches.";
                return false;
            }

            if (RequiresRuntimeUrl(context) && string.IsNullOrWhiteSpace(context.runtimeUrl))
            {
                reason = "runtimeUrl is required for online launches unless launchedFromCache is true.";
                return false;
            }

            if (context.launchedFromCache && string.IsNullOrWhiteSpace(context.resolvedVersionId))
            {
                reason = "offline cache launch requires a cached resolvedVersionId.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static bool IsExternalOnlineLaunch(LaunchContext context)
        {
            if (context == null)
            {
                return false;
            }

            return context.source == LaunchSource.ReactNativeBridge ||
                   !string.IsNullOrWhiteSpace(context.runtimeUrl) ||
                   context.launchedFromCache;
        }

        public static bool RequiresRuntimeUrl(LaunchContext context)
        {
            return IsExternalOnlineLaunch(context) && (context == null || !context.launchedFromCache);
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
