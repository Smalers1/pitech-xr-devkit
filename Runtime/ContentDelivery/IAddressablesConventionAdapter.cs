namespace Pitech.XR.ContentDelivery
{
    /// <summary>
    /// Optional extension hook for project-specific naming and metadata conventions.
    /// </summary>
    public interface IAddressablesConventionAdapter
    {
        string AdapterId { get; }

        string BuildGroupName(AddressablesModuleConfig config, string labId);

        bool TryParseLabId(string groupName, out string labId);

        void ApplyReportConventions(PublishTransactionReportData report);
    }
}
