using System;
using UnityEngine;

namespace Pitech.XR.ContentDelivery
{
    /// <summary>
    /// Optional receiver for RN/UaaL bridge payload handoff.
    /// Native bridge code can call ReceiveLaunchContextJson.
    /// </summary>
    [AddComponentMenu("Pi tech XR/Content Delivery/Bridge Launch Context Receiver")]
    public sealed class BridgeLaunchContextReceiver : MonoBehaviour
    {
        [Serializable]
        private sealed class BridgeLaunchVersioning
        {
            public string resolvedVersionId = string.Empty;
        }

        [Serializable]
        private sealed class BridgeLaunchDelivery
        {
            public string runtimeUrl = string.Empty;
        }

        [Serializable]
        private sealed class BridgeLaunchPayload
        {
            public string contractVersion = string.Empty;
            public string launchRequestId = string.Empty;
            public string attempt_id = string.Empty;
            public string attemptId = string.Empty;
            public string idempotency_key = string.Empty;
            public string idempotencyKey = string.Empty;
            public string lab_id = string.Empty;
            public string labId = string.Empty;
            public string addressKey = string.Empty;
            public string requestedAt = string.Empty;
            public bool launchedFromCache;
            public BridgeLaunchVersioning versioning = new BridgeLaunchVersioning();
            public BridgeLaunchDelivery delivery = new BridgeLaunchDelivery();
        }

        public void ReceiveLaunchContextJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            LaunchContext context = JsonUtility.FromJson<LaunchContext>(json) ?? new LaunchContext();
            BridgeLaunchPayload payload = JsonUtility.FromJson<BridgeLaunchPayload>(json);

            if (payload != null)
            {
                context.contractVersion = FirstNonEmpty(payload.contractVersion, context.contractVersion);
                context.launchRequestId = FirstNonEmpty(payload.launchRequestId, context.launchRequestId);
                context.attemptId = FirstNonEmpty(payload.attempt_id, payload.attemptId, context.attemptId);
                context.idempotencyKey = FirstNonEmpty(payload.idempotency_key, payload.idempotencyKey, context.idempotencyKey);
                context.labId = FirstNonEmpty(payload.lab_id, payload.labId, context.labId);
                context.addressKey = FirstNonEmpty(payload.addressKey, context.addressKey);
                context.requestedAt = FirstNonEmpty(payload.requestedAt, context.requestedAt);
                context.launchedFromCache = context.launchedFromCache || payload.launchedFromCache;

                if (payload.versioning != null)
                {
                    context.resolvedVersionId = FirstNonEmpty(payload.versioning.resolvedVersionId, context.resolvedVersionId);
                }

                if (payload.delivery != null)
                {
                    context.runtimeUrl = FirstNonEmpty(payload.delivery.runtimeUrl, context.runtimeUrl);
                }
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

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i].Trim();
                }
            }

            return string.Empty;
        }
    }
}
