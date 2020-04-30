#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;

namespace DevOps.Util.DotNet
{
    using static OptionSetUtil;

    public class TimelineResult<T>
    {
        public T Value { get; }

        public TimelineRecord Record { get; }

        public TimelineTree Tree { get; }

        public string RecordName => Record.Name;

        public TimelineRecord? JobRecord => Tree.TryGetJob(Record, out var job)
            ? job
            : null;

        public string? JobName => JobRecord?.Name;

        public TimelineResult(
            T result,
            TimelineRecord record,
            TimelineTree tree)
        {
            Value = result;
            Record = record;
            Tree = tree;
        }
    }

    public sealed class DotNetQueryUtil
    {
        public DevOpsServer Server { get; }

        public DotNetQueryUtil(DevOpsServer server)
        {
            Server = server;
        }

        public Task<List<TimelineResult<(Build Build, string Line)>>> SearchTimelineAsync(
            IEnumerable<Build> builds,
            string text,
            string? name = null,
            string? task = null)
        {
            var textRegex = CreateSearchRegex(text);
            var nameRegex = CreateSearchRegex(name);
            var taskRegex = CreateSearchRegex(task);
            return SearchTimelineAsync(builds, textRegex, nameRegex, taskRegex);
        }

        public async Task<List<TimelineResult<(Build Build, string Line)>>> SearchTimelineAsync(
            IEnumerable<Build> builds,
            Regex text,
            Regex? name = null,
            Regex? task = null)
        {
            var list = new List<TimelineResult<(Build Build, string Line)>>();
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

        public IEnumerable<TimelineResult<(Build Build, string Line)>> SearchTimeline(
            Build build,
            Timeline timeline,
            string text,
            string? name = null,
            string? task = null)
        {
            var textRegex = CreateSearchRegex(text);
            var nameRegex = CreateSearchRegex(name);
            var taskRegex = CreateSearchRegex(task);
            return SearchTimeline(build, timeline, textRegex, nameRegex, taskRegex);
        }

        public IEnumerable<TimelineResult<(Build Build, string Line)>> SearchTimeline(
            Build build,
            Timeline timeline,
            Regex text,
            Regex? name = null,
            Regex? task = null)
        {
            var timelineTree = TimelineTree.Create(timeline);
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
                    yield return new TimelineResult<(Build Build, string Line)>(
                        (build, line),
                        record,
                        timelineTree);
                }
            }
        }

        public async IAsyncEnumerable<Match> SearchFileAsync(
            string uri,
            Regex regex,
            Action<Exception>? onError = null)
        {
            using var stream = await Server.HttpClient.DownloadFileStreamAsync(
                uri,
                onError).ConfigureAwait(false);
            if (stream is null)
            {
                yield break;
            }

            using var reader = new StreamReader(stream);
            do
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                var match = regex.Match(line);
                if (match.Success)
                {
                    yield return match;
                }
            } while (true);
        }

        public async Task<Match?> SearchFileForFirstMatchAsync(
            string uri,
            Regex regex,
            Action<Exception>? onError = null)
        {
            var enumerable = SearchFileAsync(uri, regex, onError);
            return await enumerable.FirstOrDefaultAsync().ConfigureAwait(false);
        }

        public async Task<bool> SearchFileForAnyMatchAsync(
            string uri,
            Regex regex,
            Action<Exception>? onError = null)
        {
            var match = await SearchFileForFirstMatchAsync(uri, regex, onError).ConfigureAwait(false);
            return match?.Success == true;
        }

        public async Task<List<TimelineRecord>> ListFailedJobs(Build build)
        {
            var timeline = await Server.GetTimelineAsync(build).ConfigureAwait(false);
            var timelineTree = TimelineTree.Create(timeline);
            return timelineTree
                .Nodes
                .Where(n => !n.TimelineRecord.IsAnySuccess() && timelineTree.IsJob(n.TimelineRecord.Id))
                .Select(x => x.TimelineRecord)
                .ToList();
        }

        public async Task<List<Build>> ListBuildsAsync(
            string project,
            int count,
            int[]? definitions = null,
            string? repositoryId = null,
            string? branchName = null,
            bool includePullRequests = false,
            DateTimeOffset? before = null,
            DateTimeOffset? after = null)
        {
            // When doing before / after comparisons always use QueueTime. The StartTime parameter
            // in REST refers to when the latest build attempt started, not the original. Using that
            // means the jobs returned can violate the before / after constraint. The queue time is
            // consistent though and can be reliably used for filtering
            BuildStatus? statusFilter = BuildStatus.Completed;
            BuildQueryOrder? queryOrder = null;
            if (before is object || after is object)
            {
                queryOrder = BuildQueryOrder.QueueTimeDescending;
                statusFilter = null;
            }

            var list = new List<Build>();
            var builds = Server.EnumerateBuildsAsync(
                project,
                definitions: definitions,
                repositoryId: repositoryId,
                branchName: branchName,
                statusFilter: statusFilter,
                queryOrder: queryOrder,
                maxTime: before,
                minTime: after);
            await foreach (var build in builds)
            {
                var isUserDriven = 
                    build.Reason == BuildReason.PullRequest || 
                    build.Reason == BuildReason.Manual;
                if (isUserDriven && !includePullRequests)
                {
                    continue;
                }

                // In the case before / after is specified we have to remove the status filter hence 
                // it's possible to get back jobs that aren't completed. Filter them out here.
                if (build.Status != BuildStatus.Completed)
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
                        includePullRequests: optionSet.IncludePullRequests,
                        before: optionSet.Before,
                        after: optionSet.After);
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
                    includePullRequests: optionSet.IncludePullRequests,
                    before: optionSet.Before,
                    after: optionSet.After);
                builds.AddRange(collection);
            }

            // Exclude out the builds that are complicating results
            foreach (var excludedBuildId in optionSet.ExcludedBuildIds)
            {
                builds = builds.Where(x => x.Id != excludedBuildId).ToList();
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
        public static Regex? CreateSearchRegex(string? pattern) =>
            pattern is null 
                ? null
                : new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public async Task<List<DotNetTestRun>> ListDotNetTestRunsAsync(Build build, params TestOutcome[] outcomes)
        {
            var testRuns = await Server.ListTestRunsAsync(build.Project.Name, build.Id).ConfigureAwait(false);
            var taskList = new List<Task<DotNetTestRun>>();
            foreach (var testRun in testRuns)
            {
                taskList.Add(GetDotNetTestRunAsync(Server, build, testRun, outcomes));
            }

            await Task.WhenAll(taskList).ConfigureAwait(false);
            var list = new List<DotNetTestRun>();
            foreach (var task in taskList)
            {
                list.Add(task.Result);
            }

            return list;

            static async Task<DotNetTestRun> GetDotNetTestRunAsync(DevOpsServer server, Build build, TestRun testRun, TestOutcome[] outcomes)
            {
                var all = await server.ListTestResultsAsync(build.Project.Name, testRun.Id, outcomes: outcomes).ConfigureAwait(false);
                var info = new DotNetTestRunInfo(build, testRun);
                var list = ToDotNetTestCaseResult(info, all.ToList());
                return new DotNetTestRun(info, new ReadOnlyCollection<DotNetTestCaseResult>(list));
            }

            static List<DotNetTestCaseResult> ToDotNetTestCaseResult(DotNetTestRunInfo testRunInfo, List<TestCaseResult> testCaseResults)
            {
                var list = new List<DotNetTestCaseResult>();
                foreach (var testCaseResult in testCaseResults)
                {
                    var helixInfo = HelixUtil.TryGetHelixInfo(testCaseResult);
                    if (helixInfo is null)
                    {
                        list.Add(new DotNetTestCaseResult(testRunInfo, testCaseResult));
                        continue;
                    }

                    if (HelixUtil.IsHelixWorkItem(testCaseResult))
                    {
                        var helixWorkItem = new HelixWorkItem(testRunInfo, helixInfo.Value, testCaseResult);
                        list.Add(new DotNetTestCaseResult(testRunInfo, helixWorkItem, testCaseResult));
                    }
                    else
                    {
                        var workItemTestCaseResult = testCaseResults.FirstOrDefault(x => HelixUtil.IsHelixWorkItemAndTestCaseResult(workItem: x, test: testCaseResult));
                        if (workItemTestCaseResult is null)
                        {
                            // This can happen when helix errors and doesn't fully upload a result. Treat it like
                            // a normal test case
                            list.Add(new DotNetTestCaseResult(testRunInfo, testCaseResult));
                        }
                        else
                        {
                            var helixWorkItem = new HelixWorkItem(testRunInfo, helixInfo.Value, workItemTestCaseResult);
                            list.Add(new DotNetTestCaseResult(testRunInfo, helixWorkItem, testCaseResult));
                        }
                    }
                }

                return list;
            }
        }

        public async Task<List<HelixWorkItem>> ListHelixWorkItemsAsync(Build build, params TestOutcome[] outcomes)
        {
            var testRuns = await ListDotNetTestRunsAsync(build, outcomes).ConfigureAwait(false);
            return ListHelixWorkItems(testRuns);
        }

        public List<HelixWorkItem> ListHelixWorkItems(List<DotNetTestRun> testRuns) =>
            testRuns
                .SelectMany(x => x.TestCaseResults)
                .Where(x => x.IsHelixWorkItem)
                .SelectNullableValue(x => x.HelixWorkItem)
                .ToList();

        public Task<List<TimelineResult<string>>> ListHelixJobs(Build build)
        {
            var buildKey = build.GetBuildKey();
            return ListHelixJobs(buildKey.Project, buildKey.Number);
        }

        public async Task<List<TimelineResult<string>>> ListHelixJobs(string project, int buildNumber)
        {
            var timeline = await Server.GetTimelineAsync(project, buildNumber).ConfigureAwait(false);
            return await ListHelixJobs(timeline);
        }

        /// <summary>
        /// Find the mapping between TimelineRecord instances and the HelixJobs. It's possible and
        /// expected that a single TimelineRecord will map to multiple HelixJobs.
        ///
        /// TODO: this method works by knowing the display name of timeline records. That is very 
        /// fragile and we should find a better way to do this.
        /// </summary>
        public async Task<List<TimelineResult<string>>> ListHelixJobs(
            Timeline timeline,
            Action<Exception>? onError = null)
        {
            var timelineTree = TimelineTree.Create(timeline);

            // TODO: this scheme really relies on this name. This is pretty fragile. Should work with
            // core-eng to find a more robust way of detecting this
            var comparer = StringComparer.OrdinalIgnoreCase;
            var regex = new Regex(@"Sent Helix Job ([\d\w-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var list = new List<TimelineResult<string>>();
            foreach (var record in timelineTree.Records.Where(x => comparer.Equals(x.Name, "Send to Helix")))
            {
                var match = await SearchFileForFirstMatchAsync(record.Log.Url, regex, onError).ConfigureAwait(false);
                if (match?.Success == true)
                {
                    var id = match.Groups[1].Value;
                    list.Add(new TimelineResult<string>(id, record, timelineTree));
                }
            }

            return list;
        }

    }
}