using DevOps.Util;
using DevOps.Util.Triage;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGeneration.Contracts.Messaging;
using Octokit;
using Org.BouncyCastle.Math.EC.Rfc7748;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevOps.Status.Util
{
    public sealed class TimelineIssuesDisplay
    {
        public static TimelineIssuesDisplay Empty { get; } = new TimelineIssuesDisplay(
            new List<TimelineIssueDisplayData>(),
            includeBuildColumn: false,
            includeIssueTypeColumn: false,
            includeAttemptColumn: false);

        public List<TimelineIssueDisplayData> Issues { get; } = new List<TimelineIssueDisplayData>();
        public bool IncludeBuildColumn { get; set; }
        public bool IncludeAttemptColumn { get; set; }
        public bool IncludeIssueTypeColumn { get; set; }

        public TimelineIssuesDisplay(
            List<TimelineIssueDisplayData> issues,
            bool includeBuildColumn, 
            bool includeAttemptColumn,
            bool includeIssueTypeColumn)
        {
            Issues = issues.ToList();
            IncludeBuildColumn = includeBuildColumn;
            IncludeIssueTypeColumn = includeIssueTypeColumn;
            IncludeAttemptColumn = includeAttemptColumn;
        }

        public async Task<TimelineIssuesDisplay> Create(
            IQueryable<ModelTimelineIssue> query,
            bool includeBuildColumn,
            bool includeAttemptColumn,
            bool includeIssueTypeColumn)
        {
            var results = await query.ToListAsync();
            var issues = results.Select(x => new TimelineIssueDisplayData()
            {
                BuildNumber = x.ModelBuild.BuildNumber,
                Message = x.Message,
                JobName = x.JobName,
                IssueType = x.IssueType.ToString(),
                Attempt = x.Attempt,
            }).ToList();
            return new TimelineIssuesDisplay(issues, includeBuildColumn, includeAttemptColumn, includeIssueTypeColumn);
        }
    }

    public sealed class TimelineIssueDisplayData
    {
        public int BuildNumber { get; set; }
        public string? Message { get; set; }
        public string? JobName { get; set; }
        public string? IssueType { get; set; }
        public int Attempt { get; set; }
    }

}
