using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Pitech.XR.ContentDelivery
{
    [Serializable]
    public sealed class AttemptIdentity
    {
        public string attemptId = string.Empty;
        public string idempotencyKey = string.Empty;
        public string launchRequestId = string.Empty;
        public bool isLocalOnly = true;
        public bool isReconciled;
        public string canonicalAttemptId = string.Empty;
        public string createdAt = string.Empty;
        public string reconciledAt = string.Empty;
    }

    /// <summary>
    /// Local-first attempt identity generation and reconciliation storage.
    /// </summary>
    public static class AttemptIdentityManager
    {
        private static readonly Dictionary<string, AttemptIdentity> ByLaunchRequestId =
            new Dictionary<string, AttemptIdentity>(StringComparer.Ordinal);

        public static AttemptIdentity CreateLocalFirst(string labId)
        {
            string cleanLabId = Normalize(labId);
            string launchRequestId = Guid.NewGuid().ToString();
            string attemptId = Guid.NewGuid().ToString();
            string idempotencyKey = $"attempt:{cleanLabId}:{attemptId}";

            AttemptIdentity identity = new AttemptIdentity
            {
                attemptId = attemptId,
                idempotencyKey = idempotencyKey,
                launchRequestId = launchRequestId,
                isLocalOnly = true,
                createdAt = Timestamp.UtcNowIso8601(),
            };
            ByLaunchRequestId[launchRequestId] = identity;
            return identity;
        }

        public static bool TryGet(string launchRequestId, out AttemptIdentity identity)
        {
            if (string.IsNullOrWhiteSpace(launchRequestId))
            {
                identity = null;
                return false;
            }

            return ByLaunchRequestId.TryGetValue(launchRequestId.Trim(), out identity);
        }

        public static bool TryReconcile(string launchRequestId, string canonicalAttemptId)
        {
            if (!TryGet(launchRequestId, out AttemptIdentity identity))
            {
                return false;
            }

            identity.canonicalAttemptId = canonicalAttemptId == null ? string.Empty : canonicalAttemptId.Trim();
            identity.isReconciled = !string.IsNullOrWhiteSpace(identity.canonicalAttemptId);
            identity.isLocalOnly = !identity.isReconciled;
            identity.reconciledAt = identity.isReconciled ? Timestamp.UtcNowIso8601() : string.Empty;
            return identity.isReconciled;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            return Regex.Replace(value.Trim(), @"[^a-zA-Z0-9\-_]", "-", RegexOptions.CultureInvariant);
        }
    }
}
