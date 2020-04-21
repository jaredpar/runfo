#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        new GitHubIssueKey(timelineQuery.GitHubOrganization, timelineQuery.GitHubRepository, timelineQuery.IssueNumber);

    internal static GitHubPullRequestKey? GetGitHubPullRequestKey(ModelBuild build) =>
        build.PullRequestNumber.HasValue
            ? (GitHubPullRequestKey?)new GitHubPullRequestKey(build.GitHubOrganization, build.GitHubRepository, build.PullRequestNumber.Value)
            : null;

    internal static BuildDefinitionInfo GetBuildDefinitionInfo(ModelBuildDefinition buildDefinition) =>
        new BuildDefinitionInfo(
            buildDefinition.AzureOrganization,
            buildDefinition.AzureProject,
            buildDefinition.DefinitionName,
            buildDefinition.DefinitionId);

    internal static BuildKey GetBuildKey(ModelBuild build) =>
        new BuildKey(build.ModelBuildDefinition.AzureOrganization, build.ModelBuildDefinition.AzureProject, build.BuildNumber);

    internal static BuildInfo GetBuildInfo(ModelBuild build) =>
        new BuildInfo(
            GetBuildKey(build),
            GetBuildDefinitionInfo(build.ModelBuildDefinition),
            GetGitHubPullRequestKey(build));

    internal bool IsProcessed(ModelTimelineQuery timelineQuery, BuildKey buildKey)
    {
        var modelBuildId = GetModelBuildId(buildKey);
        var query =
            from item in Context.ModelTimelineItems
            where item.ModelBuildId == modelBuildId
            select item.Id;
        return query.Any();
    }

    internal ModelBuildDefinition GetOrCreateBuildDefinition(BuildDefinitionInfo definitionInfo)
    {
        var buildDefinition = Context.ModelBuildDefinitions
            .Where(x =>
                x.AzureOrganization == definitionInfo.Organization &&
                x.AzureProject == definitionInfo.Project &&
                x.DefinitionId == definitionInfo.Id)
            .FirstOrDefault();
        if (buildDefinition is object)
        {
            return buildDefinition;
        }

        buildDefinition = new ModelBuildDefinition()
        {
            AzureOrganization = definitionInfo.Organization,
            AzureProject = definitionInfo.Project,
            DefinitionId = definitionInfo.Id,
            DefinitionName = definitionInfo.Name,
        };

        Context.ModelBuildDefinitions.Add(buildDefinition);
        Context.SaveChanges();
        return buildDefinition;
    }

    internal ModelBuild GetOrCreateBuild(BuildInfo buildInfo)
    {
        var modelBuildId = GetModelBuildId(buildInfo.Key);
        var modelBuild = Context.ModelBuilds
            .Where(x => x.Id == modelBuildId)
            .FirstOrDefault();
        if (modelBuild is object)
        {
            return modelBuild;
        }

        var prKey = buildInfo.PullRequestKey;
        modelBuild = new ModelBuild()
        {
            Id = modelBuildId,
            ModelBuildDefinitionId = GetOrCreateBuildDefinition(buildInfo.DefinitionInfo).Id,
            GitHubOrganization = prKey?.Organization,
            GitHubRepository = prKey?.Repository,
            PullRequestNumber = prKey?.Number,
        };
        Context.ModelBuilds.Add(modelBuild);
        Context.SaveChanges();
        return modelBuild;
    }

    public bool TryGetTimelineQuery(GitHubIssueKey issueKey, [NotNullWhen(true)] out ModelTimelineQuery timelineQuery)
    {
        timelineQuery = Context.ModelTimelineQueries
            .Where(x => 
                x.GitHubOrganization == issueKey.Organization &&
                x.GitHubRepository == issueKey.Repository &&
                x.IssueNumber == issueKey.Number)
            .FirstOrDefault();
        return timelineQuery is object;
    }

    public bool TryCreateTimelineQuery(IssueKind kind, GitHubIssueKey issueKey, string text)
    {
        if (TryGetTimelineQuery(issueKey, out var timelineQuery))
        {
            return false;
        }

        timelineQuery = new ModelTimelineQuery()
        {
            GitHubOrganization = issueKey.Organization,
            GitHubRepository = issueKey.Repository,
            IssueNumber = issueKey.Number,
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
            ModelBuild = GetOrCreateBuild(result.Build.GetBuildInfo()),
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
