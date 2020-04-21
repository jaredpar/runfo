using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Model;

internal enum IssueKind
{
    Azure,
    Helix,

    NuGet,

    // General infrastructure owned by the .NET Team
    Infra,

    Build,
    Test,
    Other
}


// TODO: this class is designed to work when there is only one DB writer 
// occurring. That's a design flaw. Need to fix.
internal sealed class TriageUtil : IDisposable
{
    internal TriageDbContext Context { get; }

    internal TriageUtil()
    {
        Context = new TriageDbContext();
    }

    public void Dispose()
    {
        Context.Dispose();
    }

    internal bool IsProcessed(BuildKey buildKey)
    {
        var processedBuild = Context.ProcessedBuilds
            .Where(x => 
                x.AzureOrganization == buildKey.Organization &&
                x.AzureProject == buildKey.Project &&
                x.BuildNumber == buildKey.Id)
            .FirstOrDefault();
        return processedBuild is object;
    }

    public bool TryCreateTimelineIssue(IssueKind kind, GitHubIssueKey issueKey, string text)
    {
        var id = $"{issueKey.Organization}-{issueKey.Repository}-{issueKey.Id}";
        var timelineIssue = new TimelineIssue()
        {
            Id = id,
            GitHubOrganization = issueKey.Organization,
            GitHubRepository = issueKey.Repository,
            IssueId = issueKey.Id,
            SearchText = text
        };

        try
        {
            Context.TimelineIssues.Add(timelineIssue);
            Context.SaveChanges();
            return true;
        }
        catch
        {
            return false;
        }

    }

    public bool TryCreateTimelineEntry(TimelineIssue timelineIssue, SearchTimelineResult result)
    {
        var buildKey = result.Build.GetBuildKey();
        var entry = new TimelineEntry()
        {
            BuildKey = TriageModelUtil.GetBuildKeyId(buildKey),
            AzureOrganization = buildKey.Organization,
            AzureProject = buildKey.Project,
            BuildNumber = buildKey.Id,
            TimelineRecordName = result.TimelineRecord.Name,
            Line = result.Line,
        };

        try
        {
            Context.TimelineEntries.Add(entry);
            Context.SaveChanges();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
