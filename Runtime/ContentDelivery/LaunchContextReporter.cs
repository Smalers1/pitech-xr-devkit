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

        [Tooltip("Logs emitted payloads for debugging.")]
        public bool logPayloads;

        private IContentDeliveryService service;
        private bool subscribed;

        private void Start()
        {
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
            if (service == null || !service.TryGetCurrentContext(out LaunchContext context) || context == null)
            {
                return;
            }

            LaunchLifecyclePayload payload = BuildPayload(
                context,
                "experience_abandoned",
                sessionDurationSeconds);
            EmitPayload(payload);
        }

        private void HandleLaunchContextResolved(LaunchContext context)
        {
            if (context == null)
            {
                return;
            }

            LaunchLifecyclePayload payload = BuildPayload(context, "launch_resolved", 0f);
            EmitPayload(payload);
        }

        private static LaunchLifecyclePayload BuildPayload(
            LaunchContext context,
            string eventName,
            float sessionDurationSeconds)
        {
            LaunchLifecyclePayload payload = new LaunchLifecyclePayload
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
            return payload;
        }

        private void EmitPayload(LaunchLifecyclePayload payload)
        {
            string json = JsonUtility.ToJson(payload);
            if (logPayloads)
            {
                Debug.Log($"[ContentDelivery] Lifecycle payload: {json}", this);
            }
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
