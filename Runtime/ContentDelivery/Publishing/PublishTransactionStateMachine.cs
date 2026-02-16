using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Pitech.XR.ContentDelivery
{
    /// <summary>
    /// Transition validator for publish transaction lifecycle.
    /// </summary>
    public static class PublishTransactionStateMachine
    {
        private static readonly Dictionary<string, HashSet<string>> AllowedTransitions =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                {
                    PublishTransactionState.Draft,
                    NewSet(PublishTransactionState.Validating, PublishTransactionState.Cancelled)
                },
                {
                    PublishTransactionState.Validating,
                    NewSet(PublishTransactionState.Validated, PublishTransactionState.FailedTerminal)
                },
                {
                    PublishTransactionState.Validated,
                    NewSet(PublishTransactionState.BuildRequested, PublishTransactionState.Cancelled)
                },
                {
                    PublishTransactionState.BuildRequested,
                    NewSet(PublishTransactionState.Building, PublishTransactionState.Cancelled)
                },
                {
                    PublishTransactionState.Building,
                    NewSet(
                        PublishTransactionState.Built,
                        PublishTransactionState.FailedRetryable,
                        PublishTransactionState.FailedTerminal)
                },
                {
                    PublishTransactionState.Built,
                    NewSet(PublishTransactionState.PublishRequested, PublishTransactionState.Cancelled)
                },
                {
                    PublishTransactionState.PublishRequested,
                    NewSet(PublishTransactionState.Publishing, PublishTransactionState.Cancelled)
                },
                {
                    PublishTransactionState.Publishing,
                    NewSet(
                        PublishTransactionState.Published,
                        PublishTransactionState.FailedRetryable,
                        PublishTransactionState.FailedTerminal)
                },
                {
                    PublishTransactionState.Published,
                    NewSet(PublishTransactionState.IngestRequested, PublishTransactionState.Cancelled)
                },
                {
                    PublishTransactionState.IngestRequested,
                    NewSet(PublishTransactionState.Ingesting, PublishTransactionState.Cancelled)
                },
                {
                    PublishTransactionState.Ingesting,
                    NewSet(
                        PublishTransactionState.Ingested,
                        PublishTransactionState.FailedRetryable,
                        PublishTransactionState.FailedTerminal)
                },
                {
                    PublishTransactionState.Ingested,
                    NewSet(PublishTransactionState.ActivateRequested, PublishTransactionState.Cancelled)
                },
                {
                    PublishTransactionState.ActivateRequested,
                    NewSet(
                        PublishTransactionState.Activated,
                        PublishTransactionState.FailedRetryable,
                        PublishTransactionState.FailedTerminal)
                },
                {
                    PublishTransactionState.FailedRetryable,
                    NewSet(
                        PublishTransactionState.BuildRequested,
                        PublishTransactionState.PublishRequested,
                        PublishTransactionState.IngestRequested,
                        PublishTransactionState.ActivateRequested,
                        PublishTransactionState.Cancelled)
                },
                {
                    PublishTransactionState.Activated,
                    NewSet()
                },
                {
                    PublishTransactionState.FailedTerminal,
                    NewSet()
                },
                {
                    PublishTransactionState.Cancelled,
                    NewSet()
                },
            };

        public static bool CanTransition(string fromState, string toState)
        {
            if (string.IsNullOrEmpty(fromState) || string.IsNullOrEmpty(toState))
            {
                return false;
            }

            return AllowedTransitions.TryGetValue(fromState, out HashSet<string> next) && next.Contains(toState);
        }

        public static bool TryTransition(
            PublishTransactionReportData report,
            string toState,
            string reason,
            string actor)
        {
            if (report == null || string.IsNullOrEmpty(toState))
            {
                return false;
            }

            string from = report.state;
            if (!CanTransition(from, toState))
            {
                return false;
            }

            report.state = toState;
            report.updatedAt = Timestamp.UtcNowIso8601();
            report.stateHistory.Add(new PublishStateHistoryEntry
            {
                fromState = from,
                toState = toState,
                at = report.updatedAt,
                reason = reason ?? string.Empty,
                actor = actor ?? string.Empty,
            });

            return true;
        }

        private static HashSet<string> NewSet(params string[] values)
        {
            return new HashSet<string>(values ?? Array.Empty<string>(), StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Deterministic idempotency-key helper for publish transactions.
    /// </summary>
    public static class PublishTransactionIdempotency
    {
        public static string BuildKey(
            string tenantId,
            string labId,
            string labVersionId,
            string contentHash)
        {
            string canonical = string.Join(
                ":",
                "publish",
                Normalize(tenantId),
                Normalize(labId),
                Normalize(labVersionId),
                Normalize(contentHash));

            return canonical.ToLowerInvariant();
        }

        public static string ComputeContentFingerprint(string input)
        {
            string safe = input ?? string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(safe);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(bytes);
            return ToLowerHex(hash);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        }

        private static string ToLowerHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            char[] chars = new char[bytes.Length * 2];
            const string map = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++)
            {
                int b = bytes[i];
                chars[i * 2] = map[b >> 4];
                chars[i * 2 + 1] = map[b & 0x0F];
            }

            return new string(chars);
        }
    }

    public static class PublishTransactionFactory
    {
        public static PublishTransactionReportData CreateDraft(string source, string actor)
        {
            string now = Timestamp.UtcNowIso8601();
            PublishTransactionReportData report = new PublishTransactionReportData
            {
                transactionId = Guid.NewGuid().ToString(),
                createdAt = now,
                updatedAt = now,
                source = string.IsNullOrWhiteSpace(source)
                    ? PublishTransactionSource.GuidedSetup
                    : source.Trim(),
                state = PublishTransactionState.Draft,
            };
            report.stateHistory.Add(new PublishStateHistoryEntry
            {
                fromState = string.Empty,
                toState = PublishTransactionState.Draft,
                at = now,
                reason = "initialization",
                actor = actor ?? string.Empty,
            });
            return report;
        }
    }

    public static class Timestamp
    {
        public static string UtcNowIso8601()
        {
            return DateTime.UtcNow.ToString("o");
        }
    }
}
