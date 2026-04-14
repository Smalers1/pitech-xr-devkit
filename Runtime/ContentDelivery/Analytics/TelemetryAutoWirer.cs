using UnityEngine;

namespace Pitech.XR.ContentDelivery
{
    /// <summary>
    /// Automatically wires a <see cref="RuntimeTelemetryAdapter"/> to any
    /// <see cref="LaunchContextReporter"/> in the scene whose telemetryAdapter
    /// reference is null.  Place on any active GameObject in the scene (or add
    /// to an existing DevKit prefab).  Runs in Awake so the adapter is ready
    /// before LaunchContextReporter.Start() looks for it.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [AddComponentMenu("Pi tech XR/Analytics/Telemetry Auto Wirer")]
    public sealed class TelemetryAutoWirer : MonoBehaviour
    {
        [Tooltip("Device type stamped in attempt payloads (e.g. 'ar', 'vr').")]
        public string deviceType = "ar";

        [Tooltip("Log telemetry JSON payloads for debugging.")]
        public bool logPayloads;

        [Tooltip("Automatically emit an abandoned attempt when the adapter GameObject is destroyed.")]
        public bool emitAbandonedOnDestroy = true;

        private void Awake()
        {
            LaunchContextReporter[] reporters = FindObjectsOfType<LaunchContextReporter>(true);
            if (reporters.Length == 0)
            {
                Debug.Log("[TelemetryAutoWirer] No LaunchContextReporter found — skipping.");
                return;
            }

            int wired = 0;
            for (int i = 0; i < reporters.Length; i++)
            {
                LaunchContextReporter reporter = reporters[i];
                if (reporter.telemetryAdapter != null)
                {
                    continue;
                }

                RuntimeTelemetryAdapter existing = reporter.GetComponent<RuntimeTelemetryAdapter>();
                if (existing != null)
                {
                    reporter.telemetryAdapter = existing;
                    Debug.Log($"[TelemetryAutoWirer] Linked existing adapter on '{reporter.gameObject.name}'.");
                    wired++;
                    continue;
                }

                RuntimeTelemetryAdapter adapter = reporter.gameObject.AddComponent<RuntimeTelemetryAdapter>();
                adapter.deviceType = string.IsNullOrWhiteSpace(deviceType) ? "ar" : deviceType;
                adapter.logPayloads = logPayloads;
                adapter.autoTrackScenarioSteps = true;
                adapter.autoEmitCompletedOnScenarioFinish = true;
                adapter.emitAbandonedOnDestroy = emitAbandonedOnDestroy;
                adapter.autoFlushStepEvents = true;

                reporter.telemetryAdapter = adapter;

                Debug.Log($"[TelemetryAutoWirer] Added RuntimeTelemetryAdapter to '{reporter.gameObject.name}' (deviceType={adapter.deviceType}).");
                wired++;
            }

            if (wired > 0)
            {
                Debug.Log($"[TelemetryAutoWirer] Wired {wired} reporter(s).");
            }
            else
            {
                Debug.Log("[TelemetryAutoWirer] All reporters already have adapters assigned.");
            }
        }
    }
}
