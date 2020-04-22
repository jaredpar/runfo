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

namespace DevOps.Util.DotNet
{
    using static OptionSetUtil;

    public class SearchTimelineResult
    {
        public Build Build { get; }
        public TimelineRecord TimelineRecord { get; }
        public string Line { get; }

        public SearchTimelineResult(
            Build build,
            TimelineRecord timelineRecord,
            string line)
        {
            Build = build;
            TimelineRecord = timelineRecord;
            Line = line;
        }
    }

    public sealed class DotNetQueryUtil
    {
        public DevOpsServer Server { get; }

        public DotNetQueryUtil(DevOpsServer server)
        {
            Server = server;
        }

        public Task<List<SearchTimelineResult>> SearchTimelineAsync(
            IEnumerable<Build> builds,
            string text,
            string? name = null,
            string? task = null)
        {
            var textRegex = CreateTimelineRegex(text);
            var nameRegex = CreateTimelineRegex(name);
            var taskRegex = CreateTimelineRegex(task);
            return SearchTimelineAsync(builds, textRegex, nameRegex, taskRegex);
        }

        public async Task<List<SearchTimelineResult>> SearchTimelineAsync(
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

                list.AddRange(SearchTimeline(build, timeline, text, name, task));
            }

            return list;
        }

        public IEnumerable<SearchTimelineResult> SearchTimeline(
            Build build,
            Timeline timeline,
            string text,
            string? name = null,
            string? task = null)
        {
            var textRegex = CreateTimelineRegex(text);
            var nameRegex = CreateTimelineRegex(name);
            var taskRegex = CreateTimelineRegex(task);
            return SearchTimeline(build, timeline, textRegex, nameRegex, taskRegex);
        }

        public IEnumerable<SearchTimelineResult> SearchTimeline(
            Build build,
            Timeline timeline,
            Regex text,
            Regex? name = null,
            Regex? task = null)
        {
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
                    yield return new SearchTimelineResult(build, record, line);
                }
            }
        }

        public async Task<List<Build>> ListBuildsAsync(
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

        public async Task<List<Build>> ListBuildsAsync(string buildQuery)
        {
            var optionSet = new BuildSearchOptionSet();
            if (optionSet.Parse(buildQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Count != 0)
            {
                throw CreateBadOptionException();
            }

            return await ListBuildsAsync(optionSet).ConfigureAwait(false);
        }

        public async Task<List<Build>> ListBuildsAsync(BuildSearchOptionSet optionSet)
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
                    if (!DotNetUtil.TryGetDefinitionId(definition, defaultProject: project, out var definitionProject, out var definitionId))
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

        public static bool TryGetBuildId(BuildSearchOptionSet optionSet, string build, [NotNullWhen(true)] out string? project, out int buildId)
        {
            var defaultProject = optionSet.Project ?? BuildSearchOptionSet.DefaultProject;
            return TryGetBuildId(build, defaultProject, out project, out buildId);
        }

        public static bool TryGetBuildId(string build, string defaultProject, [NotNullWhen(true)] out string? project, out int buildId)
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

        [return: NotNullIfNotNull("pattern")]
        private static Regex? CreateTimelineRegex(string? pattern) =>
            pattern is null 
                ? null
                : new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}