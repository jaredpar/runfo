#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using static OptionUtil;

internal class SearchTimelineResult
{
    internal Build Build { get; }
    internal TimelineRecord TimelineRecord { get; }
    internal string Line { get; }

    internal SearchTimelineResult(
        Build build,
        TimelineRecord timelineRecord,
        string line)
    {
        Build = build;
        TimelineRecord = timelineRecord;
        Line = line;
    }
}

internal sealed class RuntimeQueryUtil
{
    internal DevOpsServer Server { get; }

    internal RuntimeQueryUtil(DevOpsServer server)
    {
        Server = server;
    }

    internal Task<List<SearchTimelineResult>> SearchTimelineAsync(
        IEnumerable<Build> builds,
        string text,
        string? name = null,
        string? task = null)
    {
        var options = RegexOptions.Compiled | RegexOptions.IgnoreCase;
        var textRegex = new Regex(text, options);
        Regex? nameRegex = null;
        if (name is object)
        {
            nameRegex = new Regex(name, options);
        }

        Regex? taskRegex = null;
        if (task is object)
        {
            taskRegex = new Regex(task, options);
        }

        return SearchTimelineAsync(builds, textRegex, nameRegex, taskRegex);
    }

    internal async Task<List<SearchTimelineResult>> SearchTimelineAsync(
        IEnumerable<Build> builds,
        Regex text,
        Regex? name = null,
        Regex? task = null)
    {
        var list = new List<SearchTimelineResult>();
        foreach (var build in builds)
        {
            var timeline = await Server.GetTimelineAsync(build.Project.Name, build.Id).ConfigureAwait(false);
            if (timeline is null)
            {
                continue;
            }

            var records = timeline.Records
                .Where(r => name is null || name.IsMatch(r.Name))
                .Where(r => r.Task is null || task is null || task.IsMatch(r.Task.Name));
            foreach (var record in records)
            {
                if (record.Issues is null)
                {
                    continue;
                }

                string? line = null;
                foreach (var issue in record.Issues)
                {
                    if (text.IsMatch(issue.Message))
                    {
                        line = issue.Message;
                        break;
                    }
                }

                if (line is object)
                {
                    list.Add(new SearchTimelineResult(build, record, line));
                }
            }
        }

        return list;
    }

    internal async Task<List<Build>> ListBuildsAsync(
        string project,
        int count,
        int[]? definitions = null,
        string? repositoryId = null,
        string? branchName = null,
        bool includePullRequests = false)
    {
        var list = new List<Build>();
        var builds = Server.EnumerateBuildsAsync(
            project,
            definitions: definitions,
            repositoryId: repositoryId,
            branchName: branchName,
            statusFilter: BuildStatus.Completed,
            queryOrder: BuildQueryOrder.FinishTimeDescending);
        await foreach (var build in builds)
        {
            var isUserDriven = 
                build.Reason == BuildReason.PullRequest || 
                build.Reason == BuildReason.Manual;
            if (isUserDriven && !includePullRequests)
            {
                continue;
            }

            list.Add(build);

            if (list.Count >= count)
            {
                break;
            }
        }

        return list;
    }

    internal async Task<List<Build>> ListBuildsAsync(string buildQuery)
    {
        var optionSet = new BuildSearchOptionSet();
        if (optionSet.Parse(buildQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Count != 0)
        {
            throw CreateBadOptionException();
        }

        return await ListBuildsAsync(optionSet).ConfigureAwait(false);
    }

    internal async Task<List<Build>> ListBuildsAsync(BuildSearchOptionSet optionSet)
    {
        if (optionSet.BuildIds.Count > 0 && optionSet.Definitions.Count > 0)
        {
            OptionFailure("Cannot specify builds and definitions", optionSet);
            throw CreateBadOptionException();
        }

        var project = optionSet.Project ?? BuildSearchOptionSet.DefaultProject;
        var searchCount = optionSet.SearchCount ?? BuildSearchOptionSet.DefaultSearchCount;
        var repository = optionSet.Repository;
        var branch = optionSet.Branch;
        var before = optionSet.Before;
        var after = optionSet.After;

        if (branch is object && !branch.StartsWith("refs"))
        {
            branch = $"refs/heads/{branch}";
        }

        var builds = new List<Build>();
        if (optionSet.BuildIds.Count > 0)
        {
            if (optionSet.Repository is object)
            {
                OptionFailure("Cannot specify builds and repository", optionSet);
                throw CreateBadOptionException();
            }

            if (optionSet.Branch is object)
            {
                OptionFailure("Cannot specify builds and branch", optionSet);
                throw CreateBadOptionException();
            }

            if (optionSet.SearchCount is object)
            {
                OptionFailure("Cannot specify builds and count", optionSet);
                throw CreateBadOptionException();
            }

            foreach (var buildInfo in optionSet.BuildIds)
            {
                if (!TryGetBuildId(optionSet, buildInfo, out var buildProject, out var buildId))
                {
                    OptionFailure($"Cannot convert {buildInfo} to build id", optionSet);
                    throw CreateBadOptionException();
                }

                var build = await Server.GetBuildAsync(buildProject, buildId).ConfigureAwait(false);
                builds.Add(build);
            }
        }
        else if (optionSet.Definitions.Count > 0)
        {
            foreach (var definition in optionSet.Definitions)
            {
                if (!TryGetDefinitionId(definition, out var definitionProject, out var definitionId))
                {
                    OptionFailureDefinition(definition, optionSet);
                    throw CreateBadOptionException();
                }

                definitionProject ??= project;
                var collection = await ListBuildsAsync(
                    definitionProject,
                    searchCount,
                    definitions: new[] { definitionId },
                    repositoryId: repository,
                    branchName: branch,
                    includePullRequests: optionSet.IncludePullRequests);
                builds.AddRange(collection);
            }
        }
        else
        {
            var collection = await ListBuildsAsync(
                project,
                searchCount,
                definitions: null,
                repositoryId: repository,
                branchName: branch,
                includePullRequests: optionSet.IncludePullRequests);
            builds.AddRange(collection);
        }

        // Exclude out the builds that are complicating results
        foreach (var excludedBuildId in optionSet.ExcludedBuildIds)
        {
            builds = builds.Where(x => x.Id != excludedBuildId).ToList();
        }

        // When doing before / after comparisons always use QueueTime. The StartTime parameter
        // in REST refers to when the latest build attempt started, not the original. Using that
        // means the jobs returned can violate the before / after constraint. The queue time is
        // consistent though and can be reliably used for filtering
        if (before.HasValue)
        {
            builds = builds.Where(b => b.GetQueueTime() is DateTimeOffset d && d <= before.Value).ToList();
        }

        if (after.HasValue)
        {
            builds = builds.Where(b => b.GetQueueTime() is DateTimeOffset d && d >= after.Value).ToList();
        }

        return builds;
    }

    internal async Task<BuildTestInfoCollection> ListBuildTestInfosAsync(BuildSearchOptionSet optionSet, bool includeAllTests = false)
    {
        TestOutcome[]? outcomes = includeAllTests
            ? null
            : new[] { TestOutcome.Failed };

        var list = new List<BuildTestInfo>();
        foreach (var build in await ListBuildsAsync(optionSet).ConfigureAwait(false))
        {
            try
            {
                var collection = await DotNetUtil.ListDotNetTestRunsAsync(Server, build, outcomes);
                var buildTestInfo = new BuildTestInfo(build, collection.SelectMany(x => x.TestCaseResults).ToList());
                list.Add(buildTestInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cannot get test info for {build.Id} {DevOpsUtil.GetBuildUri(build)}");
                Console.WriteLine(ex.Message);
            }
        }

        return new BuildTestInfoCollection(new ReadOnlyCollection<BuildTestInfo>(list));
    }

    internal static bool TryGetBuildId(BuildSearchOptionSet optionSet, string build, [NotNullWhen(true)] out string? project, out int buildId)
    {
        var defaultProject = optionSet.Project ?? BuildSearchOptionSet.DefaultProject;
        return TryGetBuildId(build, defaultProject, out project, out buildId);
    }

    internal static bool TryGetBuildId(string build, string defaultProject, [NotNullWhen(true)] out string? project, out int buildId)
    {
        project = null;

        var index = build.IndexOf(':');
        if (index >= 0)
        {
            var both = build.Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
            build = both[0];
            project = both[1];
        }
        else
        {
            project = defaultProject;
        }

        return int.TryParse(build, out buildId);
    }

    internal static bool TryGetDefinitionId(string definition, [NotNullWhen(true)] out string? project, out int definitionId)
    {
        definitionId = 0;
        project = null;

        if (definition is null)
        {
            return false;
        }

        var index = definition.IndexOf(':');
        if (index >= 0)
        {
            var both = definition.Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
            definition = both[0];
            project = both[1]!;
        }

        if (int.TryParse(definition, out definitionId))
        {
            return true;
        }

        foreach (var (name, p, id) in RuntimeInfo.BuildDefinitions)
        {
            if (name == definition)
            {
                definitionId = id;
                project = p;
                return true;
            }
        }

        return false;
    }

}
