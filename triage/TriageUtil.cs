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
// occurring. That's a design flaw. Need to fix for the cases that matter
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

    internal static string GetModelBuildId(BuildKey buildKey) => 
        $"{buildKey.Organization}-{buildKey.Project}-{buildKey.Number}";

    internal static GitHubIssueKey GetGitHubIssueKey(ModelTimelineQuery timelineQuery) =>
        new GitHubIssueKey(timelineQuery.GitHubOrganization, timelineQuery.GitHubRepository, timelineQuery.IssueId);

    internal bool IsProcessed(ModelTimelineQuery timelineQuery, BuildKey buildKey)
    {
        var modelBuildId = GetModelBuildId(buildKey);
        var query =
            from item in Context.ModelTimelineItems
            where item.ModelBuildId == modelBuildId
            select item.Id;
        return query.Any();
    }

    internal ModelBuild GetOrCreateBuild(BuildKey buildKey)
    {
        var modelBuildId = GetModelBuildId(buildKey);
        var modelBuild = Context.ModelBuilds
            .Where(x => x.Id == modelBuildId)
            .FirstOrDefault();
        if (modelBuild is object)
        {
            return modelBuild;
        }

        modelBuild = new ModelBuild()
        {
            Id = modelBuildId,
            AzureOrganization = buildKey.Organization,
            AzureProject = buildKey.Project,
            BuildNumber = buildKey.Number
        };
        Context.ModelBuilds.Add(modelBuild);
        Context.SaveChanges();
        return modelBuild;
    }

    public bool TryCreateTimelineQuery(IssueKind kind, GitHubIssueKey issueKey, string text)
    {
        var timelineQuery = Context.ModelTimelineQueries
            .Where(x => 
                x.GitHubOrganization == issueKey.Organization &&
                x.GitHubRepository == issueKey.Repository &&
                x.IssueId == issueKey.Id)
            .FirstOrDefault();
        if (timelineQuery is object)
        {
            return false;
        }

        timelineQuery = new ModelTimelineQuery()
        {
            GitHubOrganization = issueKey.Organization,
            GitHubRepository = issueKey.Repository,
            IssueId = issueKey.Id,
            SearchText = text
        };

        try
        {
            Context.ModelTimelineQueries.Add(timelineQuery);
            Context.SaveChanges();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    public void CreateTimelineItem(ModelTimelineQuery timelineQuery, SearchTimelineResult result)
    {
        var item = new ModelTimelineItem()
        {
            TimelineRecordName = result.TimelineRecord.Name,
            Line = result.Line,
            ModelBuild = GetOrCreateBuild(result.Build.GetBuildKey()),
            ModelTimelineQuery = timelineQuery,
            BuildNumber = result.Build.GetBuildKey().Number,
        };

        try
        {
            Context.ModelTimelineItems.Add(item);
            Context.SaveChanges();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
