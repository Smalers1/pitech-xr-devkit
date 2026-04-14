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

        /// <summary>
        /// Validates a <see cref="RuntimeTelemetryAttemptPayload"/> against the
        /// PRD minimum required fields before it is emitted.  Returns false with
        /// a human-readable reason when a required field is missing.
        /// </summary>
        public static bool TryValidateAttemptPayload(
            RuntimeTelemetryAttemptPayload payload,
            out string reason)
        {
            reason = string.Empty;
            if (payload == null)
            {
                reason = "attempt payload is null";
                return false;
            }

            if (string.IsNullOrWhiteSpace(payload.attempt_id))
            {
                reason = "attempt_id is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(payload.idempotency_key))
            {
                reason = "idempotency_key is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(payload.lab_id))
            {
                reason = "lab_id is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(payload.completion_status))
            {
                reason = "completion_status is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(payload.lab_version_id))
            {
                reason = "lab_version_id (resolvedVersionId) is required for completed attempts";
                return false;
            }

            if (payload.duration_seconds < 0)
            {
                reason = "duration_seconds must be >= 0";
                return false;
            }

            if (payload.critical_error_count < 0)
            {
                reason = "critical_error_count must be >= 0";
                return false;
            }

            return true;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
