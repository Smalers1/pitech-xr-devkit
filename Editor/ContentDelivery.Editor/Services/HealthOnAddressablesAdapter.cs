#if UNITY_EDITOR
using System.Text.RegularExpressions;

namespace Pitech.XR.ContentDelivery.Editor
{
    /// <summary>
    /// HealthOn-specific convention adapter kept outside the generic runtime core.
    /// </summary>
    public sealed class HealthOnAddressablesAdapter : IAddressablesConventionAdapter
    {
        public string AdapterId => "healthon";

        public string BuildGroupName(AddressablesModuleConfig config, string labId)
        {
            string normalized = Normalize(labId);
            return $"lab_{normalized}";
        }

        public bool TryParseLabId(string groupName, out string labId)
        {
            labId = string.Empty;
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return false;
            }

            Match match = Regex.Match(groupName.Trim(), @"^lab_(?<lab>[a-zA-Z0-9\-_]+)$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            labId = Normalize(match.Groups["lab"].Value);
            return !string.IsNullOrWhiteSpace(labId);
        }

        public void ApplyReportConventions(PublishTransactionReportData report)
        {
            if (report == null)
            {
                return;
            }

            report.addressables.groupPolicy = "one_remote_group_per_lab";
            report.runtimePolicy.allowOfflineCacheLaunch = true;
            report.runtimePolicy.allowOlderCachedSameLab = true;
            report.runtimePolicy.networkRequiredIfCacheMiss = true;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "default";
            }

            return Regex.Replace(value.Trim(), @"[^a-zA-Z0-9\-_]", "-", RegexOptions.CultureInvariant);
        }
    }
}
#endif
