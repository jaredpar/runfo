using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using DevOps.Util;
using DevOps.Util.DotNet;
using Newtonsoft.Json.Serialization;
using Octokit;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DevOps.Util.DotNet
{
    using static OptionSetUtil;

    public class TimelineRecordItem
    {
        public TimelineRecord Record { get; }

        public TimelineTree Tree { get; }

        public string RecordName => Record.Name;

        public TimelineRecord? JobRecord => Tree.TryGetJob(Record, out var job)
            ? job
            : null;

        public string? JobName => JobRecord?.Name;

        public TimelineRecord? RootRecord => Tree.TryGetRoot(Record, out var record)
            ? record
            : null;

        public TimelineRecordItem(
            TimelineRecord record,
            TimelineTree tree)
        {
            Debug.Assert(tree.TryGetNode(record.Id, out _));
            Record = record;
            Tree = tree;
        }
    }

    public sealed class SearchTimelineResult
    {
        public TimelineRecordItem Record { get; }

        public BuildInfo BuildInfo { get; }

        public string Line { get; }

        public SearchTimelineResult(
            TimelineRecordItem record,
            BuildInfo buildInfo,
            string line)
        {
            Record = record;
            BuildInfo = buildInfo;
            Line = line;
        }
    }

    public sealed class HelixTimelineResult
    {
        public TimelineRecordItem Record { get; }

        public HelixJobTimelineInfo HelixJob { get; }

        public HelixTimelineResult(
            TimelineRecordItem record,
            HelixJobTimelineInfo helixJob)
        {
            Record = record;
            HelixJob = helixJob;
        }
    }

    public sealed class DotNetQueryUtil
    {
        public DevOpsServer Server { get; }
        public IAzureUtil AzureUtil { get; }

        public DotNetQueryUtil(DevOpsServer server, IAzureUtil azureUtil)
        {
            if (server.Organization != azureUtil.Organization)
            {
                throw new ArgumentException();
            }

            Server = server;
            AzureUtil = azureUtil;
        }

        public Task<List<SearchTimelineResult>> SearchTimelineAsync(
            IEnumerable<BuildInfo> buildInfos,
            string text,
            string? name = null,
            string? task = null,
            int? attempt = null,
            Action<Exception>? onError = null)
        {
            var textRegex = CreateSearchRegex(text);
            var nameRegex = CreateSearchRegex(name);
            var taskRegex = CreateSearchRegex(task);
            return SearchTimelineAsync(buildInfos, textRegex, nameRegex, taskRegex, attempt, onError);
        }

        public async Task<List<SearchTimelineResult>> SearchTimelineAsync(
            IEnumerable<BuildInfo> buildInfos,
            Regex text,
            Regex? name = null,
            Regex? task = null,
            int? attempt = null,
            Action<Exception>? onError = null)
        {
            var list = new List<SearchTimelineResult>();
            foreach (var buildInfo in buildInfos)
            {
                Timeline? timeline = null;
                try
                {
                    timeline = await AzureUtil.GetTimelineAttemptAsync(buildInfo.Project, buildInfo.Number, attempt).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                    timeline = null;
                }

                if (timeline is null)
                {
                    continue;
                }

                list.AddRange(SearchTimeline(buildInfo, timeline, text, name, task));
            }

            return list;
        }

        public IEnumerable<SearchTimelineResult> SearchTimeline(
            BuildInfo buildInfo,
            Timeline timeline,
            string text,
            string? name = null,
            string? task = null,
            int? attempt = null)
        {
            var textRegex = CreateSearchRegex(text);
            var nameRegex = CreateSearchRegex(name);
            var taskRegex = CreateSearchRegex(task);
            return SearchTimeline(buildInfo, timeline, textRegex, nameRegex, taskRegex);
        }

        public IEnumerable<SearchTimelineResult> SearchTimeline(
            BuildInfo buildInfo,
            Timeline timeline,
            Regex text,
            Regex? name = null,
            Regex? task = null,
            int? attempt = null)
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
                    yield return new SearchTimelineResult(
                        new TimelineRecordItem(record, timelineTree),
                        buildInfo,
                        line);
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
            if (timeline is null)
            {
                return new List<TimelineRecord>();
            }

            var timelineTree = TimelineTree.Create(timeline);
            return timelineTree
                .Nodes
                .Where(n => !n.TimelineRecord.IsAnySuccess() && timelineTree.IsJob(n.TimelineRecord.Id))
                .Select(x => x.TimelineRecord)
                .ToList();
        }

        public Task<List<Build>> ListBuildsAsync(
            int count = 50,
            string? project = null,
            string? definition = null,
            string? repositoryName = null,
            string? branch = null,
            bool includePullRequests = false,
            string? before = null,
            string? after = null)
        {
            string? repositoryId = null;
            if (repositoryName is object)
            {
                repositoryId = $"{DotNetUtil.GitHubOrganization}/{repositoryName}";
            }

            int[]? definitions = null;
            if (definition is object)
            {
                if (!DotNetUtil.TryGetDefinitionId(definition, out _, out var definitionId))
                {
                    throw new Exception($"Invalid definition name {definition}");
                }

                definitions = new[] { definitionId };
            }

            DateTimeOffset? beforeDateTimeOffset = null;
            if (before is object)
            {
                beforeDateTimeOffset = DateTimeOffset.Parse(before);
            }

            DateTimeOffset? afterDateTimeOffset = null;
            if (after is object)
            {
                afterDateTimeOffset = DateTimeOffset.Parse(after);
            }

            return ListBuildsAsync(
                count: count,
                project: project,
                definitions: definitions,
                repositoryId: repositoryId,
                branch: branch,
                includePullRequests: includePullRequests,
                before: beforeDateTimeOffset,
                after: afterDateTimeOffset);
        }

        public async Task<List<Build>> ListBuildsAsync(
            int count,
            string? project = null,
            int[]? definitions = null,
            string? repositoryId = null,
            string? branch = null,
            bool includePullRequests = false,
            DateTimeOffset? before = null,
            DateTimeOffset? after = null)
        {
            project ??= DotNetUtil.DefaultAzureProject;

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
                branchName: branch,
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

        public static List<string> TokenizeQuery(string query)
        {
            var list = new List<string>();
            var builder = new StringBuilder();
            var start = 0;
            var index = 0;
            var inQuote = false;
            while (index < query.Length)
            {
                var current = query[index];
                if (current == '"')
                {
                    if (inQuote)
                    {
                        builder.Append('"');
                        inQuote = false;
                        CompleteItem();
                    }
                    else
                    {
                        builder.Append('"');
                        inQuote = true;
                        index++;
                    }
                }
                else if (!inQuote && char.IsWhiteSpace(current))
                {
                    CompleteItem();
                }
                else
                {
                    builder.Append(current);
                    index++;
                }
            }

            CompleteItem();

            return list;

            void CompleteItem()
            {
                if (builder.Length > 0)
                {
                    var item = builder.ToString();
                    if (!string.IsNullOrEmpty(item))
                    {
                        list.Add(item);
                    }

                    builder.Length = 0;
                }

                start = index + 1;
                index++;
            }
        }

        public static IEnumerable<(string Name, string Value)> TokenizeQueryPairs(string query)
        {
            foreach (var item in TokenizeQuery(query))
            {
                if (item.Contains(':'))
                {
                    var split = item.Split(new[] { ':' }, count: 2);
                    yield return (split[0], split[1]);
                }
                else
                {
                    yield return ("", item);
                }
            }
        }

        public async Task<List<Build>> ListBuildsAsync(BuildSearchOptionSet optionSet)
        {
            if (optionSet.BuildIds.Count > 0 && optionSet.Definitions.Count > 0)
            {
                OptionFailure("Cannot specify builds and definitions", optionSet);
                throw CreateBadOptionException();
            }

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
            else
            {
                var (project, definitions) = GetProjectAndDefinitions();
                var collection = await ListBuildsAsync(
                    searchCount,
                    project,
                    definitions: definitions,
                    repositoryId: repository,
                    branch: branch,
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

            (string Project, int[] Definitions) GetProjectAndDefinitions()
            {
                if (optionSet.Definitions.Count == 0)
                {
                    return (optionSet.Project ?? DotNetUtil.DefaultAzureProject, Array.Empty<int>());
                }

                string? project = null;
                var list = new List<int>();
                foreach (var definition in optionSet.Definitions)
                {
                    if (!DotNetUtil.TryGetDefinitionId(definition, out var definitionProject, out var definitionId))
                    {
                        OptionFailureDefinition(definition, optionSet);
                        throw CreateBadOptionException();
                    }

                    if (definitionProject is object)
                    {
                        if (project is null)
                        {
                            project = definitionProject;
                        }
                        else if (!StringComparer.OrdinalIgnoreCase.Equals(definitionProject, project))
                        {
                            throw new InvalidOperationException($"Conflicting project names {project} and {definitionProject}");
                        }
                    }

                    list.Add(definitionId);
                }

                project ??= DotNetUtil.DefaultAzureProject;
                return (project, list.ToArray());
            }
        }

        public static bool TryGetBuildId(BuildSearchOptionSet optionSet, string build, [NotNullWhen(true)] out string? project, out int buildId)
        {
            var defaultProject = optionSet.Project ?? DotNetUtil.DefaultAzureProject;
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

        // TODO: need to get rid of all these overloads that take project + build number or 
        // Build or BuildInfo. Need a type that combines them together. Also should consider one
        // that encapsulates the attempt
        public async Task<List<DotNetTestRun>> ListDotNetTestRunsAsync(Build build, params TestOutcome[] outcomes)
        {
            var testRuns = await AzureUtil.ListTestRunsAsync(build.Project.Name, build.Id).ConfigureAwait(false);
            var taskList = new List<Task<DotNetTestRun>>();
            foreach (var testRun in testRuns)
            {
                taskList.Add(GetDotNetTestRunAsync(build, testRun, outcomes));
            }

            await Task.WhenAll(taskList).ConfigureAwait(false);
            var list = new List<DotNetTestRun>();
            foreach (var task in taskList)
            {
                list.Add(task.Result);
            }

            return list;

        }

        public async Task<DotNetTestRun> GetDotNetTestRunAsync(Build build, TestRun testRun, TestOutcome[] outcomes)
        {
            var buildInfo = build.GetBuildInfo();
            var all = await AzureUtil.ListTestResultsAsync(buildInfo.Project, testRun.Id, outcomes: outcomes).ConfigureAwait(false);
            var info = new DotNetTestRunInfo(build, testRun);
            var list = ToDotNetTestCaseResult(info, all.ToList());
            return new DotNetTestRun(info, new ReadOnlyCollection<DotNetTestCaseResult>(list));

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

        public async Task<List<HelixTimelineResult>> ListHelixJobsAsync(string project, int buildNumber, int? attempt = null)
        {
            var timeline = await AzureUtil.GetTimelineAttemptAsync(project, buildNumber, attempt).ConfigureAwait(false);
            if (timeline is null)
            {
                return new List<HelixTimelineResult>();
            }

            return await ListHelixJobsAsync(timeline);
        }

        /// <summary>
        /// Find the mapping between TimelineRecord instances and the HelixJobs. It's possible and
        /// expected that a single TimelineRecord will map to multiple HelixJobs.
        ///
        /// TODO: this method works by knowing the display name of timeline records. That is very 
        /// fragile and we should find a better way to do this.
        /// </summary>
        public async Task<List<HelixTimelineResult>> ListHelixJobsAsync(
            Timeline timeline,
            Action<Exception>? onError = null)
        {
            var timelineTree = TimelineTree.Create(timeline);

            // TODO: this scheme really relies on this name. This is pretty fragile. Should work with
            // core-eng to find a more robust way of detecting this
            var comparer = StringComparer.OrdinalIgnoreCase;
            var comparison = StringComparison.OrdinalIgnoreCase;
            var sentRegex = new Regex(@"Sent Helix Job ([\d\w-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var queueRegex = new Regex(@"Sending Job to (.*)\.\.\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var list = new List<HelixTimelineResult>();
            var helixRecords = timelineTree.Records.Where(x => 
                comparer.Equals(x.Name, "Send to Helix") ||
                comparer.Equals(x.Name, "Send tests to Helix") ||
                x.Name.StartsWith("Run native crossgen and compare", comparison));
            foreach (var record in helixRecords)
            {
                if (record.Log is null)
                {
                    continue;
                }
 
                using var stream = await Server.HttpClient.DownloadFileStreamAsync(
                    record.Log.Url,
                    onError).ConfigureAwait(false);
                if (stream is null)
                {
                    continue;
                }

                var jobName = timelineTree.TryGetJob(record, out var job)
                    ? job.Name
                    : "";

                string? queueName = null;
                string? containerName = null;
                string? containerImage = null;
                using var reader = new StreamReader(stream);
                do
                {
                    var line = reader.ReadLine();
                    if (line is null)
                    {
                        break;
                    }

                    var match = queueRegex.Match(line);
                    if (match.Success)
                    {
                        queueName = match.Groups[1].Value;
                        containerImage = null;
                        containerName = null;
                        match = Regex.Match(queueName, @"\(([\w\d.-]+)\)?([\w\d.-]+)@(.*)");
                        if (match.Success)
                        {
                            queueName = match.Groups[2].Value;
                            containerName = match.Groups[1].Value;
                            containerImage = match.Groups[3].Value;
                        }
                        continue;
                    }

                    match = sentRegex.Match(line);
                    if (match.Success)
                    {
                        var id = match.Groups[1].Value;
                        var machineInfo = new MachineInfo(
                            queueName ?? MachineInfo.UnknownHelixQueueName,
                            jobName,
                            containerName,
                            containerImage,
                            isHelixSubmission: true);
                        var info = new HelixJobTimelineInfo(id, machineInfo);
                        list.Add(new HelixTimelineResult(
                            new TimelineRecordItem(record, timelineTree),
                            info));
                    }
                } while (true);
            }

            return list;
        }

        public async Task<List<MachineInfo>> ListBuildMachineInfoAsync(
            string project,
            int buildNumber,
            int? attempt = null,
            bool includeAzure = true,
            bool includeHelix = true)
        {
            var list = new List<MachineInfo>();
            if (!includeAzure && !includeHelix)
            {
                return list;
            }

            if (includeAzure)
            {
                await GetJobMachineInfoAsync().ConfigureAwait(false);
            }

            if (includeHelix)
            {
                await GetHelixMachineInfoAsync().ConfigureAwait(false);
            }

            return list;

            async Task GetJobMachineInfoAsync()
            {
                var yaml = await Server.GetYamlAsync(project, buildNumber).ConfigureAwait(false);
                var parser = new Parser(new StringReader(yaml));
                var yamlStream = new YamlStream();
                yamlStream.Load(parser);
                var document = yamlStream.Documents[0];

                foreach (var mapping in document.AllNodes.OfType<YamlMappingNode>())
                {
                    if (!TryGetScalarValue(mapping, "job", out var jobName))
                    {
                        continue;
                    }

                    if (TryGetScalarValue(mapping, "displayName", out var displayNameValue))
                    {
                        jobName = displayNameValue;
                    }

                    if (jobName is null)
                    {
                        continue;
                    }

                    string? container = null;
                    if (TryGetNode<YamlMappingNode>(mapping, "container", out var containerNode) &&
                        TryGetScalarValue(containerNode, "alias", out var alias))
                    {
                        container = alias;
                    }

                    string? queue = null;
                    if (TryGetNode<YamlMappingNode>(mapping, "pool", out var poolNode))
                    {
                        if (TryGetScalarValue(poolNode, "vmImage", out var vmName))
                        {
                            queue = vmName;
                        }
                        else if (TryGetScalarValue(poolNode, "queue", out var queueName))
                        {
                            queue = queueName;
                        }
                        else if (container is object)
                        {
                            queue = MachineInfo.UnknownContainerQueueName;
                        }
                    }

                    if (queue is object)
                    {
                        list.Add(new MachineInfo(
                            queue,
                            jobName,
                            container,
                            container,
                            isHelixSubmission: false));
                    }
                }

                bool TryGetNode<T>(YamlMappingNode node, string name, out T childNode)
                    where T : YamlNode
                {
                    if (node.Children.TryGetValue(name, out var n) &&
                        n is T t)
                    {
                        childNode = t;
                        return true;
                    }

                    childNode = null!;
                    return false;
                }

                bool TryGetScalarValue(YamlMappingNode node, string name, out string value)
                {
                    if (TryGetNode<YamlScalarNode>(node, name, out var scalarNode))
                    {
                        value = scalarNode.Value!;
                        return true;
                    }

                    value = null!;
                    return false;
                }
            }

            async Task GetHelixMachineInfoAsync()
            {
                var helixJobs = await ListHelixJobsAsync(project, buildNumber, attempt).ConfigureAwait(false);

                foreach (var item in helixJobs)
                {
                    list.Add(item.HelixJob.MachineInfo);
                }
            }
        }
    }
}
