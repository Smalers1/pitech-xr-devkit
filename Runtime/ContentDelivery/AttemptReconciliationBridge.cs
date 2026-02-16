using Pitech.XR.Core;
using UnityEngine;

namespace Pitech.XR.ContentDelivery
{
    /// <summary>
    /// Optional bridge entry point for local-first attempt reconciliation.
    /// </summary>
    [AddComponentMenu("Pi tech XR/Content Delivery/Attempt Reconciliation Bridge")]
    public sealed class AttemptReconciliationBridge : MonoBehaviour
    {
        public bool logReconciliation;

        public bool ReconcileAttempt(string launchRequestId, string canonicalAttemptId)
        {
            IContentDeliveryService service = XRServices.Get<IContentDeliveryService>();
            bool reconciled = service != null && service.TryReconcileAttempt(launchRequestId, canonicalAttemptId);

            if (logReconciliation)
            {
                Debug.Log(
                    $"[ContentDelivery] Reconcile launchRequestId={launchRequestId}, canonicalAttemptId={canonicalAttemptId}, success={reconciled}",
                    this);
            }

            return reconciled;
        }
    }
}
