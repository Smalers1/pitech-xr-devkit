using System;
using Pitech.XR.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Pitech.XR.ContentDelivery
{
    [Serializable]
    public sealed class LaunchResolvedData
    {
        public string labId = string.Empty;
        public string addressKey = string.Empty;
        public string resolvedVersionId = string.Empty;
        public string runtimeUrl = string.Empty;
        public float sessionDurationSeconds;
        public long durationMs;
    }

    [Serializable]
    public sealed class LaunchLifecyclePayload
    {
        public string contractVersion = "1.1.0";
        public string launchRequestId = string.Empty;
        public string attempt_id = string.Empty;
        public string idempotency_key = string.Empty;
        public string timestamp = string.Empty;
        public string @event = string.Empty;
        public LaunchResolvedData data = new LaunchResolvedData();
    }

    /// <summary>
    /// Emits normalized launch lifecycle payloads as JSON for bridge consumers.
    /// </summary>
    [AddComponentMenu("Pi tech XR/Content Delivery/Launch Context Reporter")]
    public sealed class LaunchContextReporter : MonoBehaviour
    {
        [Tooltip("JSON payload callback for bridge handlers.")]
        public UnityEvent<string> onLifecycleJson;

        [Tooltip("Optional analytics adapter to emit attempt-end payloads.")]
        public RuntimeTelemetryAdapter telemetryAdapter;

        [Tooltip("Logs emitted payloads for debugging.")]
        public bool logPayloads;

        private IContentDeliveryService service;
        private bool subscribed;

        private void Start()
        {
            if (telemetryAdapter == null)
            {
                telemetryAdapter = GetComponent<RuntimeTelemetryAdapter>();
            }
            TrySubscribe();
        }

        private void Update()
        {
            if (!subscribed)
            {
                TrySubscribe();
            }
        }

        private void OnDestroy()
        {
            if (service != null)
            {
                service.OnLaunchContextResolved -= HandleLaunchContextResolved;
            }
        }

        public void EmitExperienceAbandoned(float sessionDurationSeconds)
        {
            telemetryAdapter?.EmitAttemptAbandoned(sessionDurationSeconds);

            if (service == null || !service.TryGetCurrentContext(out LaunchContext context) || context == null)
            {
                return;
            }

            if (!TryBuildPayload(context, "experience_abandoned", sessionDurationSeconds, out LaunchLifecyclePayload payload, out string reason))
            {
                Debug.LogWarning($"[ContentDelivery] Skipping lifecycle payload: {reason}", this);
                return;
            }

            EmitPayload(payload);
        }

        private void HandleLaunchContextResolved(LaunchContext context)
        {
            if (context == null)
            {
                return;
            }

            if (!TryBuildPayload(context, "launch_resolved", 0f, out LaunchLifecyclePayload payload, out string reason))
            {
                Debug.LogWarning($"[ContentDelivery] Skipping lifecycle payload: {reason}", this);
                return;
            }

            EmitPayload(payload);
        }

        private static bool TryBuildPayload(
            LaunchContext context,
            string eventName,
            float sessionDurationSeconds,
            out LaunchLifecyclePayload payload,
            out string reason)
        {
            payload = null;
            if (!LaunchContextValidation.TryValidateLineage(context, requireResolvedVersionId: true, out reason))
            {
                return false;
            }

            if (LaunchContextValidation.RequiresRuntimeUrl(context) && string.IsNullOrWhiteSpace(context.runtimeUrl))
            {
                reason = "runtimeUrl is required for online lifecycle events.";
                return false;
            }

            payload = new LaunchLifecyclePayload
            {
                contractVersion = string.IsNullOrWhiteSpace(context.contractVersion) ? "1.1.0" : context.contractVersion,
                launchRequestId = context.launchRequestId,
                attempt_id = context.attemptId,
                idempotency_key = context.idempotencyKey,
                timestamp = Timestamp.UtcNowIso8601(),
                @event = eventName,
                data = new LaunchResolvedData
                {
                    labId = context.labId,
                    addressKey = context.addressKey,
                    resolvedVersionId = context.resolvedVersionId,
                    runtimeUrl = context.runtimeUrl,
                    sessionDurationSeconds = sessionDurationSeconds,
                    durationMs = (long)(sessionDurationSeconds * 1000f),
                }
            };

            reason = string.Empty;
            return true;
        }

        private void EmitPayload(LaunchLifecyclePayload payload)
        {
            string json = JsonUtility.ToJson(payload);
            if (logPayloads)
            {
                Debug.Log($"[ContentDelivery] Lifecycle payload: {json}", this);
            }

            AndroidUnityBridgeEmitter.EmitLifecycleJson(json);
            onLifecycleJson?.Invoke(json);
        }

        private void TrySubscribe()
        {
            service = XRServices.Get<IContentDeliveryService>();
            if (service == null || subscribed)
            {
                return;
            }

            service.OnLaunchContextResolved += HandleLaunchContextResolved;
            subscribed = true;
            if (service.TryGetCurrentContext(out LaunchContext context))
            {
                HandleLaunchContextResolved(context);
            }
        }
    }
}

