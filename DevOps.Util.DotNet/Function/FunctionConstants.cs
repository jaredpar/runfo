using System;
using System.Collections.Generic;
using System.Text;

namespace DevOps.Util.DotNet.Function
{
    public static class FunctionConstants
    {
        public const string QueueNameBuildComplete = "build-complete";
        public const string QueueNameBuildRetry = "build-retry";
        public const string QueueNameTriageBuildAttempt = "triage-build-attempt";
        public const string QueueNameTriageBuild = "triage-build";
        public const string QueueNameTriageTrackingIssue = "triage-tracking-issue";
        public const string QueueNamePullRequestMerged = "pull-request-merged";
        public const string QueueNameIssueUpdateManual = "issue-update-manual";
    }
}
