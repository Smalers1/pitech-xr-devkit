using UnityEngine;

namespace Pitech.XR.ContentDelivery
{
    [CreateAssetMenu(
        menuName = "Pi tech/Content Delivery/Publish Transaction Report",
        fileName = "PublishTransactionReport")]
    public sealed class PublishTransactionReportAsset : ScriptableObject
    {
        [SerializeField] private PublishTransactionReportData data = new PublishTransactionReportData();

        public PublishTransactionReportData Data => data;

        public void Replace(PublishTransactionReportData next)
        {
            data = next ?? new PublishTransactionReportData();
        }
    }
}
