using System;
using System.Text.RegularExpressions;

namespace Pitech.XR.ContentDelivery
{
    public sealed class DefaultAddressablesConventionAdapter : IAddressablesConventionAdapter
    {
        public string AdapterId => "default";

        public string BuildGroupName(AddressablesModuleConfig config, string labId)
        {
            string normalizedLabId = NormalizeLabId(labId);
            string template = config != null && !string.IsNullOrWhiteSpace(config.groupNameTemplate)
                ? config.groupNameTemplate
                : "lab_{labId}";

            return template.Replace("{labId}", normalizedLabId);
        }

        public bool TryParseLabId(string groupName, out string labId)
        {
            labId = string.Empty;
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return false;
            }

            Match match = Regex.Match(groupName.Trim(), @"lab[_\-](?<lab>[a-zA-Z0-9\-_]+)", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            labId = NormalizeLabId(match.Groups["lab"].Value);
            return !string.IsNullOrEmpty(labId);
        }

        public void ApplyReportConventions(PublishTransactionReportData report)
        {
            if (report == null)
            {
                return;
            }

            report.addressables.groupPolicy = "one_remote_group_per_lab";
            report.ccd.provider = "ccd";
        }

        private static string NormalizeLabId(string labId)
        {
            if (string.IsNullOrWhiteSpace(labId))
            {
                return "default";
            }

            return Regex.Replace(labId.Trim(), @"[^a-zA-Z0-9\-_]", "-", RegexOptions.CultureInvariant);
        }
    }
}
