using System;
using System.Collections.Generic;
using System.Text;
using DevOps.Util.DotNet.Triage;
using Newtonsoft.Json;

namespace DevOps.Util.DotNet.Function
{
    public sealed class BuildInfoMessage
    {
        public string? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public int BuildNumber { get; set; }
    }

    public sealed class PullRequestMergedMessage
    {
        public string? Organization { get; set; }
        public string? Repository { get; set; }
        public int PullRequestNumber { get; set; }
    }

    public sealed class BuildMessage
    {
        public string? Organization { get; set; }
        public string? Project { get; set; }
        public int? BuildNumber { get; set; }

        [JsonIgnore]
        public BuildKey? BuildKey =>
            Organization is { } o && Project is { } p && BuildNumber is { } n
            ? new BuildKey(o, p, n)
            : (BuildKey?)null;

        public BuildMessage()
        {

        }

        public BuildMessage(BuildKey key)
        {
            Organization = key.Organization;
            Project = key.Project;
            BuildNumber = key.Number;
        }
    }

    public sealed class BuildAttemptMessage
    {
        public BuildMessage? BuildMessage { get; set; }
        public int? Attempt { get; set; }

        [JsonIgnore]
        public BuildKey? BuildKey => BuildMessage?.BuildKey;

        [JsonIgnore]
        public BuildAttemptKey? BuildAttemptKey =>
            BuildKey is { } key && Attempt is { } a
            ? new BuildAttemptKey(key, a)
            : (BuildAttemptKey?)null;

        public BuildAttemptMessage()
        {

        }

        public BuildAttemptMessage(BuildKey buildKey, int attempt)
        {
            BuildMessage = new BuildMessage(buildKey);
            Attempt = attempt;
        }

        public BuildAttemptMessage(BuildAttemptKey key)
        {
            BuildMessage = new BuildMessage(key.BuildKey);
            Attempt = key.Attempt;
        }
    }

    public sealed class TriageTrackingIssueRangeMessage
    {
        public int? ModelTrackingIssueId { get; set; }

        public BuildAttemptMessage[]? BuildAttemptMessages { get; set; }
    }

    public sealed class TriageTrackingIssueMessage
    {
        public BuildAttemptMessage? BuildAttemptMessage { get; set; }
        public int? ModelTrackingIssueId { get; set; }

        [JsonIgnore]
        public BuildAttemptKey? BuildAttemptKey => BuildAttemptMessage?.BuildAttemptKey;

        public TriageTrackingIssueMessage()
        {

        }

        public TriageTrackingIssueMessage(BuildAttemptKey key, int modelTrackingIssueId)
        {
            BuildAttemptMessage = new BuildAttemptMessage(key);
            ModelTrackingIssueId = modelTrackingIssueId;
        }
    }

    public sealed class IssueUpdateManualMessage
    {
        public int? ModelTrackingIssueId { get; set; }

        public IssueUpdateManualMessage(int modelTrackingIssueId)
        {
            ModelTrackingIssueId = modelTrackingIssueId;
        }
    }
}
