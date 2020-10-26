using System;
using System.Collections.Generic;
using System.Text;

namespace DevOps.Util.DotNet.Triage
{
    public sealed class SiteLinkUtil
    {
#if DEBUG
        public static SiteLinkUtil Debug { get; } = new SiteLinkUtil("localhost:44341");
#endif

        public static SiteLinkUtil Published { get; } = new SiteLinkUtil("runfo.azurewebsites.net");

        public string DomainName { get; }

        public SiteLinkUtil(string domainName)
        {
            DomainName = domainName;
        }

        public string GetTrackingIssueUri(int modelTrackingIssueId) => $"https://{DomainName}/tracking/issue/{modelTrackingIssueId}";
    }
}
