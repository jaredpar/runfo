using System;
using System.Collections.Generic;
using System.Text;

namespace DevOps.Util.DotNet.Function
{
    public static class FunctionConstants
    {
        public const string QueueNameBuildComplete = "build-complete";
        public const string QueueNameBuildRetry = "build-retry";
        public const string QueueNameTriageBuild = "triage-build";
        public const string QueueNameTriageTrackingIssue = "triage-tracking-issue";
        public const string QueueNameTriageTrackingIssueRange = "triage-tracking-issue-range";
        public const string QueueNamePullRequestMerged = "pull-request-merged";
        public const string QueueNameIssueUpdateManual = "issue-update-manual";

        public static IEnumerable<string> AllQueueNames
        {
            get
            {
                yield return QueueNameBuildComplete;
                yield return QueueNameBuildRetry;
                yield return QueueNameTriageBuild;
                yield return QueueNameTriageTrackingIssue;
                yield return QueueNameTriageTrackingIssueRange;
                yield return QueueNamePullRequestMerged;
                yield return QueueNameIssueUpdateManual;
            }
        }
    }
}
