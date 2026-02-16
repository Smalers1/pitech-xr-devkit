using System;
using System.Collections.Generic;

namespace Pitech.XR.ContentDelivery
{
    /// <summary>
    /// Canonical schema constants for DevKit publish transaction reports.
    /// </summary>
    public static class PublishTransactionSchema
    {
        public const string Version = "publish_transaction.v1";
    }

    /// <summary>
    /// Source values for transaction creation.
    /// </summary>
    public static class PublishTransactionSource
    {
        public const string GuidedSetup = "devkit_guided_setup";
        public const string HiddenBuild = "devkit_hidden_build";
    }

    /// <summary>
    /// String-based lifecycle states for report interoperability with JSON/SQL.
    /// </summary>
    public static class PublishTransactionState
    {
        public const string Draft = "draft";
        public const string Validating = "validating";
        public const string Validated = "validated";
        public const string BuildRequested = "build_requested";
        public const string Building = "building";
        public const string Built = "built";
        public const string PublishRequested = "publish_requested";
        public const string Publishing = "publishing";
        public const string Published = "published";
        public const string IngestRequested = "ingest_requested";
        public const string Ingesting = "ingesting";
        public const string Ingested = "ingested";
        public const string ActivateRequested = "activate_requested";
        public const string Activated = "activated";
        public const string FailedRetryable = "failed_retryable";
        public const string FailedTerminal = "failed_terminal";
        public const string Cancelled = "cancelled";
    }

    public static class PublishCheckSeverity
    {
        public const string Info = "info";
        public const string Warning = "warning";
        public const string Error = "error";
    }

    [Serializable]
    public sealed class PublishActorInfo
    {
        public string userId = string.Empty;
        public string machineId = string.Empty;
        public string editorVersion = string.Empty;
        public string devkitVersion = string.Empty;
    }

    [Serializable]
    public sealed class PublishLabInfo
    {
        public string tenantId = string.Empty;
        public string labId = string.Empty;
        public string labVersionId = string.Empty;
        public string versionNumber = string.Empty;
        public string scenarioSchemaVersion = string.Empty;
    }

    [Serializable]
    public sealed class PublishAddressablesInfo
    {
        public string groupPolicy = "one_remote_group_per_lab";
        public string groupName = string.Empty;
        public string profileName = string.Empty;
        public string buildTarget = string.Empty;
        public string catalogMode = string.Empty;
        public string remoteLoadPathTemplate = string.Empty;
    }

    [Serializable]
    public sealed class PublishCcdInfo
    {
        public string provider = "ccd";
        public string projectId = string.Empty;
        public string environment = string.Empty;
        public string bucketId = string.Empty;
        public string releaseId = string.Empty;
        public string badge = string.Empty;
        public string entryUrl = string.Empty;
    }

    [Serializable]
    public sealed class PublishArtifactsInfo
    {
        public string catalogHash = string.Empty;
        public string contentHash = string.Empty;
        public string reportJsonPath = string.Empty;
        public string buildOutputPath = string.Empty;
        public long bundleSizeBytes;
    }

    [Serializable]
    public sealed class PublishRuntimePolicyInfo
    {
        public bool allowOfflineCacheLaunch = true;
        public bool allowOlderCachedSameLab = true;
        public bool networkRequiredIfCacheMiss = true;
    }

    [Serializable]
    public sealed class PublishCheckEntry
    {
        public string code = string.Empty;
        public string severity = PublishCheckSeverity.Info;
        public string message = string.Empty;
        public string scope = string.Empty;
        public string fixHint = string.Empty;
        public bool passed = true;
    }

    [Serializable]
    public sealed class PublishErrorEntry
    {
        public string code = string.Empty;
        public string message = string.Empty;
        public string phase = string.Empty;
        public bool retryable;
        public bool terminal;
        public string details = string.Empty;
    }

    [Serializable]
    public sealed class PublishStateHistoryEntry
    {
        public string fromState = string.Empty;
        public string toState = string.Empty;
        public string at = string.Empty;
        public string reason = string.Empty;
        public string actor = string.Empty;
    }

    [Serializable]
    public sealed class PublishTransactionReportData
    {
        public string schemaVersion = PublishTransactionSchema.Version;
        public string transactionId = string.Empty;
        public string idempotencyKey = string.Empty;
        public string createdAt = string.Empty;
        public string updatedAt = string.Empty;
        public string source = PublishTransactionSource.GuidedSetup;
        public PublishActorInfo actor = new PublishActorInfo();
        public PublishLabInfo lab = new PublishLabInfo();
        public PublishAddressablesInfo addressables = new PublishAddressablesInfo();
        public PublishCcdInfo ccd = new PublishCcdInfo();
        public PublishArtifactsInfo artifacts = new PublishArtifactsInfo();
        public PublishRuntimePolicyInfo runtimePolicy = new PublishRuntimePolicyInfo();
        public List<PublishCheckEntry> checks = new List<PublishCheckEntry>();
        public List<PublishErrorEntry> errors = new List<PublishErrorEntry>();
        public string state = PublishTransactionState.Draft;
        public List<PublishStateHistoryEntry> stateHistory = new List<PublishStateHistoryEntry>();
    }

}
