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
using Mono.Options;
using static Runfo.RuntimeInfoUtil;
using static Runfo.OptionSetUtil;
using System.Text;
using Octokit;

namespace Runfo
{
    // TODO: Change to use SearchBuildLogsRequest
    // TODO: Change to use SearchHelixLogsRequest
    internal sealed partial class RuntimeInfo
    {
        internal DevOpsServer DevOpsServer { get; }
        
        internal HelixServer HelixServer { get; }

        internal IAzureUtil AzureUtil { get; }

        internal DotNetQueryUtil QueryUtil { get; }

        private ReportBuilder ReportBuilder { get; } = new ReportBuilder();

        internal RuntimeInfo(
            DevOpsServer devopsServer,
            HelixServer helixServer,
            IAzureUtil azureUtil)
        {
            DevOpsServer = devopsServer;
            HelixServer = helixServer;
            AzureUtil = azureUtil;
            QueryUtil = new DotNetQueryUtil(DevOpsServer, azureUtil);
        }

        internal async Task PrintBuildResults(IEnumerable<string> args)
        {
            int count = 5;
            var optionSet = new OptionSet()
        {
            { "c|count=", "count of builds to return", (int c) => count = c }
        };

            ParseAll(optionSet, args);

            var data = DotNetUtil.BuildDefinitions
                .AsParallel()
                .AsOrdered()
                .Select(async t => (t.BuildName, t.DefinitionId, await QueryUtil.ListBuildsAsync(count, t.Project, new[] { t.DefinitionId })));

            foreach (var task in data)
            {
                var (name, definitionId, builds) = await task;
                Console.Write($"{name,-20}");
                var percent = (builds.Count(x => x.Result == BuildResult.Succeeded) / (double)count) * 100;
                Console.Write($"{percent,4:G3}%  ");
                foreach (var build in builds)
                {
                    var c = build.Result == BuildResult.Succeeded ? 'Y' : 'N';
                    Console.Write(c);
                }

                Console.WriteLine();
            }
        }

        internal int ClearCache()
        {
            Directory.Delete(RuntimeInfoUtil.CacheDirectory, recursive: true);
            return ExitSuccess;
        }

        internal async Task CollectCache()
        {
            try
            {
                await Task.Yield();
                Directory.CreateDirectory(RuntimeInfoUtil.CacheDirectory);
                var now = DateTime.UtcNow;
                var limit = TimeSpan.FromDays(1);
                var filePaths = Directory.EnumerateFiles(
                    RuntimeInfoUtil.CacheDirectory,
                    "*",
                    new EnumerationOptions() { RecurseSubdirectories = true });
                foreach (var filePath in filePaths)
                {
                    await Task.Yield();
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (now - fileInfo.CreationTimeUtc >= limit)
                        {
                            File.Delete(filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting {filePath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cannot clear cache: {ex.Message}");
            }
        }

        internal async Task<int> PrintSearchHelix(IEnumerable<string> args)
        {
            string? text = null;
            bool markdown = false;
            var optionSet = new BuildSearchOptionSet()
        {
            { "v|value=", "text to search for", t => text = t },
            { "m|markdown", "print output in markdown", m => markdown = m is object },
        };

            ParseAll(optionSet, args);

            if (text is null)
            {
                Console.WriteLine("Must provide a text argument to search for");
                optionSet.WriteOptionDescriptions(Console.Out);
                return ExitFailure;
            }

            var textRegex = new Regex(text, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var collection = await QueryUtil.ListBuildsAsync(optionSet);
            var foundRaw = collection
                .AsParallel()
                .Select(async b => await SearchBuild(DevOpsServer, QueryUtil, textRegex, b));
            var found = await RuntimeInfoUtil.ToListAsync(foundRaw);
            var badLogBuilder = new StringBuilder();
            found.ForEach(x => x.BadLogs.ForEach(l => badLogBuilder.AppendLine(l)));
            Console.WriteLine(ReportBuilder.BuildSearchHelix(
                found
                    .Select(x => (x.Build.GetBuildInfo(), x.LogInfo))
                    .Where(x => x.LogInfo is object),
                new[] { HelixLogKind.Console, HelixLogKind.CoreDump },
                markdown: markdown,
                badLogBuilder.ToString()));

            return ExitSuccess;

            static async Task<(Build Build, HelixLogInfo? LogInfo, List<string> BadLogs)> SearchBuild(
                DevOpsServer server,
                DotNetQueryUtil queryUtil,
                Regex textRegex,
                Build build)
            {
                var badLogList = new List<string>();
                try
                {
                    var helixApi = HelixServer.GetHelixApi();
                    var workItems = await queryUtil
                        .ListHelixWorkItemsAsync(build, DotNetUtil.FailedTestOutcomes)
                        .ConfigureAwait(false);
                    foreach (var workItem in workItems)
                    {
                        var logInfo = await HelixUtil.GetHelixLogInfoAsync(helixApi, workItem);
                        if (logInfo.ConsoleUri is object)
                        {
                            var isMatch = await queryUtil.SearchFileForAnyMatchAsync(
                                logInfo.ConsoleUri,
                                textRegex,
                                ex => badLogList.Add($"Unable to search helix logs {build.Id} {workItem.JobId}, {logInfo.ConsoleUri}: {ex.Message}")).ConfigureAwait(false);
                            if (isMatch)
                            {
                                return (build, logInfo, badLogList);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    badLogList.Add($"Unable to search helix logs for {build.Id}:  {ex.Message}");
                }

                return (build, null, badLogList);
            }
        }

        internal async Task<int> PrintSearchTimeline(IEnumerable<string> args)
        {
            string? name = null;
            string? text = null;
            string? task = null;
            bool markdown = false;
            int? attempt = null;
            var optionSet = new BuildSearchOptionSet()
        {
            { "n|name=", "Search only records matching this name", n => name = n },
            { "t|task=", "Search only tasks matching this name", t => task = t },
            { "v|value=", "text to search for", t => text = t },
            { "a|attempt=", "attempt to search in", (int a) => attempt = a },
            { "m|markdown", "print output in markdown", m => markdown = m is object },
        };

            ParseAll(optionSet, args);

            if (text is null)
            {
                Console.WriteLine("Must provide a text argument to search for");
                optionSet.WriteOptionDescriptions(Console.Out);
                return ExitFailure;
            }

            var hadDefinition = optionSet.Definitions.Any();
            var builds = await QueryUtil.ListBuildsAsync(optionSet);
            var found = new List<SearchTimelineResult>();
            Console.WriteLine("Got builds");
            foreach (var build in builds)
            {
                try
                {
                    foreach (var timeline in await ListTimelinesAsync(build, attempt))
                    {
                        found.AddRange(QueryUtil.SearchTimeline(
                            build.GetBuildResultInfo(),
                            timeline,
                            text,
                            name,
                            task));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting timeline for {build.GetBuildInfo().BuildUri}: {ex.Message}");
                }
            }
            Console.WriteLine(ReportBuilder.BuildSearchTimeline(
                found.Select(x => (x.BuildResultInfo.BuildAndDefinitionInfo, x.Record.JobName)),
                markdown: markdown,
                includeDefinition: !hadDefinition));

            return ExitSuccess;
        }

        internal async Task<int> PrintSearchBuildLogs(IEnumerable<string> args)
        {
            string? name = null;
            string? text = null;
            bool markdown = false;
            bool trace = false;
            var optionSet = new BuildSearchOptionSet()
        {
            { "n|name=", "name regex to match in results", n => name = n },
            { "v|value=", "text to search for", t => text = t },
            { "m|markdown", "print output in markdown", m => markdown = m is object },
            { "tr|trace", "trace the records searched", t => trace = t is object },
        };

            ParseAll(optionSet, args);

            if (text is null)
            {
                Console.WriteLine("Must provide a text argument to search for");
                optionSet.WriteOptionDescriptions(Console.Out);
                return ExitFailure;
            }

            var builds = await QueryUtil.ListBuildsAsync(optionSet);
            var textRegex = new Regex(text, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Regex? nameRegex = null;
            if (name is object)
            {
                nameRegex = new Regex(name, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }

            if (markdown)
            {
                Console.WriteLine("|Build|Kind|Timeline Record|");
                Console.WriteLine("|---|---|---|");
            }

            var (found, recordCount) = await SearchBuildLogs(DevOpsServer, builds, nameRegex, textRegex, trace);
            foreach (var tuple in found)
            {
                var build = tuple.Build;
                if (markdown)
                {
                    var kind = "Rolling";
                    if (DevOpsUtil.TryGetPullRequestKey(build, out var pullRequestKey))
                    {
                        kind = $"PR {pullRequestKey.PullRequestUri}";
                    }
                    Console.WriteLine($"|[{build.Id}]({DevOpsUtil.GetBuildUri(build)})|{kind}|{tuple.TimelineRecord.Name}|");
                }
                else
                {
                    Console.WriteLine(DevOpsUtil.GetBuildUri(build));
                }
            }

            var foundBuildCount = found.GroupBy(x => x.Build.Id).Count();
            Console.WriteLine();
            Console.WriteLine($"Evaluated {builds.Count} builds");
            Console.WriteLine($"Impacted {foundBuildCount} builds");
            Console.WriteLine($"Impacted {found.Count} jobs");
            Console.WriteLine($"Searched {recordCount} records");

            return ExitSuccess;

            static async Task<(List<(Build Build, TimelineRecord TimelineRecord, string? Line)>, int Count)> SearchBuildLogs(
                DevOpsServer server,
                IEnumerable<Build> builds,
                Regex? nameRegex,
                Regex textRegex,
                bool trace)
            {
                // Deliberately iterating the build serially vs. using AsParallel because othewise we 
                // end up sending way too many requests to AzDO. Eventually it will begin rate limiting
                // us.
                var list = new List<(Build Build, TimelineRecord TimelineRecord, string? Line)>();
                var count = 0;
                foreach (var build in builds)
                {
                    var timeline = await server.GetTimelineAsync(build.Project.Name, build.Id);
                    if (timeline is null)
                    {
                        continue;
                    }
                    var records = timeline.Records.Where(r => nameRegex is null || nameRegex.IsMatch(r.Name)).ToList();
                    count += records.Count;
                    var all = (await SearchTimelineRecords(server, build, records, textRegex, trace))
                        .Select(x => (build, x.TimelineRecord, x.Line));
                    list.AddRange(all);
                }

                return (list, count);
            }

            static async Task<IEnumerable<(TimelineRecord TimelineRecord, string? Line)>> SearchTimelineRecords(
                DevOpsServer server,
                Build build,
                IEnumerable<TimelineRecord> records,
                Regex textRegex,
                bool trace)
            {
                var hashSet = new HashSet<string>();
                foreach (var record in records)
                {
                    if (record.Log is object && !hashSet.Add(record.Log.Url))
                    {
                        Console.WriteLine("duplicate");
                    }
                }

                var all = records
                    .AsParallel()
                    .Select(async r =>
                    {
                        var tuple = await SearchTimelineRecord(server, r, textRegex);
                        return (tuple.IsMatch, TimelineRecord: r, tuple.Line);
                    });

                var list = await RuntimeInfoUtil.ToListAsync(all);
                if (trace)
                {
                    Console.WriteLine(DevOpsUtil.GetBuildUri(build));
                    foreach (var tuple in list)
                    {
                        Console.WriteLine($"  {tuple.TimelineRecord.Name} {tuple.IsMatch} {tuple.Line}");
                    }
                }

                return list
                    .Where(x => x.IsMatch)
                    .Select(x => (x.TimelineRecord, x.Line));
            }

            static async Task<(bool IsMatch, string? Line)> SearchTimelineRecord(DevOpsServer server, TimelineRecord record, Regex textRegex)
            {
                if (record.Log is null)
                {
                    return (false, null);
                }

                try
                {
                    using var stream = await DownloadFile();
                    using var reader = new StreamReader(stream);
                    do
                    {
                        var line = await reader.ReadLineAsync();
                        if (line is null)
                        {
                            break;
                        }

                        if (textRegex.IsMatch(line))
                        {
                            return (true, line);
                        }

                    } while (true);
                }
                catch (Exception)
                {
                    // Handle random download fails
                }

                return (false, null);

                // TODO: Have to implement some back off here for 429 responses. This is really
                // hacky. Should centralize
                async Task<MemoryStream> DownloadFile()
                {
                    int count = 0;
                    while (true)
                    {
                        try
                        {
                            return await server.DownloadFileAsync(record.Log.Url);
                        }
                        catch
                        {
                            count++;
                            if (count > 5)
                            {
                                throw;
                            }
                            await Task.Delay(TimeSpan.FromSeconds(5));
                        }
                    }
                }
            }
        }

        internal async Task<int> PrintHelixJobs(IEnumerable<string> args)
        {
            var optionSet = new BuildSearchOptionSet();
            ParseAll(optionSet, args);

            foreach (var build in await QueryUtil.ListBuildsAsync(optionSet))
            {
                Console.WriteLine(build.GetBuildResultInfo().BuildUri);
                var jobs = await QueryUtil.ListHelixJobsAsync(build.Project.Name, build.Id);
                foreach (var group in jobs.GroupBy(x => x.Record.JobName ?? "<unknown>"))
                {
                    Console.WriteLine(group.Key);
                    foreach (var item in group)
                    {
                        Console.WriteLine($"  {item.HelixJob}");
                    }
                }
            }

            return ExitSuccess;
        }

        internal async Task<int> PrintHelix(IEnumerable<string> args)
        {
            var verbose = false;
            var optionSet = new BuildSearchOptionSet()
        {
            { "v|verbose", "verbose output", v => verbose = v is object },
        };

            ParseAll(optionSet, args);

            foreach (var buildTestInfo in await QueryUtil.ListBuildTestInfosAsync(optionSet))
            {
                var list = await GetHelixLogsAsync(buildTestInfo, includeConsoleText: verbose);
                Console.WriteLine("Console Logs");
                foreach (var (helixLogInfo, consoleText) in list.Where(x => x.Item1.ConsoleUri is object))
                {
                    Console.WriteLine($"{helixLogInfo.ConsoleUri}");
                    if (verbose)
                    {
                        Console.WriteLine(consoleText);
                    }
                }

                Console.WriteLine();
                var wroteHeader = false;
                foreach (var (helixLogInfo, _) in list.Where(x => x.HelixLogInfo.TestResultsUri is object))
                {
                    if (!wroteHeader)
                    {
                        Console.WriteLine("Test Results");
                        wroteHeader = true;
                    }
                    Console.WriteLine($"{helixLogInfo.TestResultsUri}");
                }

                Console.WriteLine();
                wroteHeader = false;
                foreach (var (helixLogInfo, _) in list.Where(x => x.HelixLogInfo.CoreDumpUri is object))
                {
                    if (!wroteHeader)
                    {
                        Console.WriteLine("Core Logs");
                        wroteHeader = true;
                    }
                    Console.WriteLine($"{helixLogInfo.CoreDumpUri}");
                }
            }

            return ExitSuccess;
        }

        private async Task<List<(HelixLogInfo HelixLogInfo, string? ConsoleText)>> GetHelixLogsAsync(BuildTestInfo buildTestInfo, bool includeConsoleText)
        {
            var helixApi = HelixServer.GetHelixApi();
            var logs = buildTestInfo
                .GetHelixWorkItems()
                .AsParallel()
                .Select(async (HelixInfoWorkItem workItem) =>
                {
                    string? consoleText = null;
                    if (includeConsoleText)
                    {
                        consoleText = await HelixUtil.GetHelixConsoleText(helixApi, workItem);
                    }

                    var helixLogInfo = await HelixUtil.GetHelixLogInfoAsync(helixApi, workItem);
                    return (helixLogInfo, consoleText);
                });

            return await RuntimeInfoUtil.ToListAsync(logs);
        }

        internal void PrintBuildDefinitions()
        {
            foreach (var (name, project, definitionId) in DotNetUtil.BuildDefinitions)
            {
                var uri = DevOpsUtil.GetDefinitionUri(DevOpsServer.Organization, project, definitionId);
                Console.WriteLine($"{name,-20}{uri}");
            }
        }

        internal async Task<int> PrintBuilds(IEnumerable<string> args)
        {
            var optionSet = new BuildSearchOptionSet();
            ParseAll(optionSet, args);

            foreach (var build in await QueryUtil.ListBuildsAsync(optionSet))
            {
                var uri = DevOpsUtil.GetBuildUri(build);
                var prId = DevOpsUtil.TryGetPullRequestKey(build, out var pullRequestKey)
                    ? (int?)pullRequestKey.Number
                    : null;
                var kind = prId.HasValue ? "PR" : "CI";
                Console.WriteLine($"{build.Id}\t{kind}\t{build.Result,-13}\t{uri}");
            }

            return ExitSuccess;
        }

        internal async Task<int> PrintArtifacts(IEnumerable<string> args)
        {
            var list = false;
            int top = 10;
            var optionSet = new BuildSearchOptionSet()
        {
            { "l|list", "detailed artifact listing", l => list = l is object },
            { "t|top=", "print top <count> artifacts in list" , (int t) => top = t},
        };

            ParseAll(optionSet, args);

            var kb = 1_024;
            var mb = kb * kb;
            var builds = await GetInfo();

            if (list)
            {
                PrintList();
            }
            else
            {
                PrintSummary();
            }

            return ExitSuccess;

            async Task<List<(Build Build, List<BuildArtifact> artifacts)>> GetInfo()
            {
                var list = new List<(Build Build, List<BuildArtifact> artifacts)>();
                foreach (var build in await QueryUtil.ListBuildsAsync(optionSet))
                {
                    var artifacts = await DevOpsServer.ListArtifactsAsync(build.Project.Name, build.Id);
                    list.Add((build, artifacts));
                }

                return list;
            }

            (double Total, double Average, double Max) GetArtifactInfo(List<BuildArtifact> artifacts)
            {
                var sizes = artifacts
                    .Select(x => x.GetByteSize() is int i ? (double?)i : null)
                    .SelectNullableValue()
                    .Select(x => x / mb);
                if (!sizes.Any())
                {
                    return (0, 0, 0);
                }

                double total = sizes.Sum();
                double average = sizes.Average();
                double max = sizes.Max();
                return (Total: total, Average: average, Max: max);
            }

            void PrintSummary()
            {
                Console.WriteLine("Build     Total      Average    Max");
                Console.WriteLine(new string('=', 50));
                foreach (var (build, artifacts) in builds)
                {
                    var (total, average, max) = GetArtifactInfo(artifacts);
                    Console.WriteLine($"{build.Id,-9} {total,-10:N2} {average,-10:N2} {max,-10:N2}");
                }
                Console.WriteLine(new string('=', 50));
                Console.WriteLine("Sizes are MB");
            }

            void PrintList()
            {
                foreach (var (build, artifacts) in builds)
                {
                    var (total, average, max) = GetArtifactInfo(artifacts);

                    Console.WriteLine(DevOpsUtil.GetBuildUri(build));
                    Console.WriteLine("Stats");
                    Console.WriteLine($"  Total   {total:N2}");
                    Console.WriteLine($"  Average {average:N2}");
                    Console.WriteLine($"  Max     {max:N2}");

                    var sorted = artifacts
                        .Where(x => x.GetByteSize().HasValue)
                        .Select(x => (Artifact: x, Size: (double)x.GetByteSize()!.Value / mb))
                        .OrderByDescending(x => x.Size)
                        .Take(top);
                    Console.WriteLine("Detailed");
                    foreach (var (artifact, size) in sorted)
                    {
                        var mark = artifact.GetKind() != BuildArtifactKind.PipelineArtifact ? '?' : ' ';
                        Console.WriteLine($"  {size,8:N2}{mark}   {artifact.Name}");
                    }
                }
            }
        }

        internal async Task<int> PrintTimeline(IEnumerable<string> args)
        {
            int? depth = null;
            bool issues = false;
            bool failed = false;
            bool verbose = false;
            bool summary = false;
            string? name = null;
            int? attempt = null;
            var optionSet = new BuildSearchOptionSet()
        {
            { "depth=", "depth to print to", (int d) => depth = d },
            { "issues", "print recrods that have issues", i => issues = i is object },
            { "failed", "print records that failed", f => failed = f is object },
            { "summary", "print summary", s => summary = s is object },
            { "n|name=", "record name to search for", n => name = n},
            { "v|verbose", "print issues with records", v => verbose = v is object },
            { "a|attempt=", "attempt to search in", (int a) => attempt = a },
        };

            ParseAll(optionSet, args);

            foreach (var build in await QueryUtil.ListBuildsAsync(optionSet))
            {
                var buildInfo = build.GetBuildResultInfo();
                var timeline = await AzureUtil.GetTimelineAttemptAsync(build.Project.Name, build.Id, attempt);
                if (timeline is null)
                {
                    Console.WriteLine($"Error: no timeline for {buildInfo.BuildUri}");
                    continue;
                }

                var tree = TimelineTree.Create(timeline);
                if (summary)
                {
                    var succeeded = !tree.Nodes.Any(x => x.TimelineRecord.IsAnyFailed())
                        ? "Succeeded"
                        : "Failed";
                    Console.WriteLine($"{buildInfo.BuildUri} {succeeded}");
                }
                else
                {
                    DumpTimeline(DevOpsServer, timeline, depth, issues, failed, verbose);
                }
            }

            static void DumpTimeline(DevOpsServer server, Timeline timeline, int? depthLimit, bool issues, bool failed, bool verbose)
            {
                var tree = TimelineTree.Create(timeline);
                if (issues)
                {
                    tree = tree.Filter(x => x.Issues is object);
                }

                if (failed)
                {
                    tree = tree.Filter(x => x.IsAnyFailed());
                }

                foreach (var rootNode in tree.RootNodes)
                {
                    DumpRecord(rootNode, 0);
                }

                void DumpRecord(TimelineTree.TimelineNode current, int depth)
                {
                    if (depth > depthLimit == true)
                    {
                        return;
                    }

                    var record = current.TimelineRecord;
                    PrintRecord(depth, record);
                    if ((issues || verbose) && record.Issues is object)
                    {
                        var indent = GetIndent(depth + 1);
                        foreach (var issue in record.Issues)
                        {
                            var category = issue.Category;
                            category = string.IsNullOrEmpty(category) ? "" : $" {category}";
                            Console.WriteLine($"{indent}{issue.Type}{category}: {issue.Message}");
                        }
                    }

                    foreach (var child in current.Children)
                    {
                        /*
                        if (record.Details is object)
                        {
                            var subTimeline = await server.GetTimelineAsync("public", buildId.Value, record.Details.Id, record.Details.ChangeId);
                            if (subTimeline is object)
                            {
                                await DumpTimeline(nextIndent, subTimeline);
                            }
                        }
                        */

                        DumpRecord(child, depth + 1);
                    }
                }

                void PrintRecord(int depth, TimelineRecord record)
                {
                    var indent = GetIndent(depth);
                    var duration = RuntimeInfoUtil.TryGetDuration(record.StartTime, record.FinishTime);
                    Console.WriteLine($"{indent}{record.Name} ({duration}) ({record.Task?.Name}) {record.Result}");
                }

                string GetIndent(int depth) => new string(' ', depth * 2);
            }

            return ExitSuccess;
        }

        internal async Task<int> PrintMachines(IEnumerable<string> args)
        {
            int? attempt = null;
            bool verbose = false;
            bool? azure = null;
            bool? helix = null;
            var optionSet = new BuildSearchOptionSet()
        {
            { "azdo", "print azdo job machines", a => azure = a is object },
            { "helix", "print helix job machines", h => helix = h is object },
            { "v|verbose", "print issues with records", v => verbose = v is object },
            { "a|attempt=", "attempt to search in", (int a) => attempt = a },
        };

            ParseAll(optionSet, args);

            if (azure is null && helix is null)
            {
                azure = true;
                helix = true;
            }

            foreach (var build in await QueryUtil.ListBuildsAsync(optionSet))
            {
                Console.WriteLine(build.GetBuildResultInfo().BuildUri);
                Console.WriteLine();
                var list = await QueryUtil.ListBuildMachineInfoAsync(build.Project.Name, build.Id, attempt, azure ?? false, helix ?? false);
                foreach (var item in list.GroupBy(x => x.FriendlyName, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key))
                {
                    Console.WriteLine($"{item.Key} ({item.Count()})");
                    if (verbose)
                    {
                        var info = item.First();
                        if (info.IsContainer)
                        {
                            Console.WriteLine($"  Image: {info.ContainerImage}");
                            Console.WriteLine($"  Queue: {info.QueueName}");
                        }

                        foreach (var child in item)
                        {
                            Console.WriteLine($"  {child.JobName}");
                        }
                    }
                }
                Console.WriteLine();
                Console.WriteLine($"Total: {list.Count}");
            }


            return ExitSuccess;
        }

        internal async Task<int> PrintBuildYaml(IEnumerable<string> args)
        {
            var optionSet = new BuildSearchOptionSet();
            ParseAll(optionSet, args);

            foreach (var build in await QueryUtil.ListBuildsAsync(optionSet))
            {
                var log = await DevOpsServer.GetYamlAsync(build.Project.Name, build.Id);
                Console.WriteLine(log);
            }

            return ExitSuccess;
        }

        internal async Task<int> PrintPullRequestBuilds(IEnumerable<string> args)
        {
            string? repository = null;
            int? number = null;
            string? definition = null;
            var optionSet = new OptionSet()
        {
            { "d|definition=", "definition to print tests for", d => definition = d },
            { "r|repository=", "repository name (dotnet/runtime)", r => repository = r },
            { "n|number=", "pull request number", (int n) => number = n },
        };

            ParseAll(optionSet, args);

            var project = DotNetUtil.DefaultAzureProject;
            IEnumerable<int>? definitions = null;
            if (definition is object)
            {
                if (!DotNetUtil.TryGetDefinitionId(definition, out string? definitionProject, out int definitionId))
                {
                    OptionSetUtil.OptionFailureDefinition(definition, optionSet);
                    return ExitFailure;
                }

                if (definitionProject is object)
                {
                    project = definitionProject;
                }
                definitions = new[] { definitionId };
            }

            if (number is null || repository is null)
            {
                Console.WriteLine("Must provide a repository and pull request number");
                optionSet.WriteOptionDescriptions(Console.Out);
                return ExitFailure;
            }

            IEnumerable<Build> builds = await DevOpsServer.ListBuildsAsync(
                project,
                definitions: definitions,
                repositoryId: repository,
                branchName: $"refs/pull/{number.Value}/merge",
                repositoryType: "github");
            builds = builds.OrderByDescending(b => b.Id);

            Console.WriteLine($"Definition           Build    Result       Url");
            foreach (var build in builds)
            {
                var name = DotNetUtil.GetDefinitionName(build);
                Console.WriteLine($"{name,-20} {build.Id,-8} {build.Result,-12} {DevOpsUtil.GetBuildUri(build)}");
            }

            return ExitSuccess;
        }

        internal async Task<int> PrintFailedTests(IEnumerable<string> args)
        {
            bool verbose = false;
            bool markdown = false;
            bool includeAllTests = false;
            string? name = null;
            string grouping = "tests";
            var optionSet = new BuildSearchOptionSet()
        {
            { "g|grouping=", "output grouping: tests*, builds, jobs", g => grouping = g },
            { "m|markdown", "output in markdown", m => markdown = m  is object },
            { "n|name=", "name regex to match in results", n => name = n },
            { "at|all-tests", "include all tests", at => includeAllTests = at is object },
            { "v|verbose", "verbose output", d => verbose = d is object },
        };

            ParseAll(optionSet, args);

            var collection = await QueryUtil.ListBuildTestInfosAsync(optionSet, includeAllTests);
            await PrintFailureInfo(collection, grouping, name, verbose, markdown);
            return ExitSuccess;
        }

        private async Task PrintFailureInfo(
            BuildTestInfoCollection collection,
            string grouping,
            string? name,
            bool verbose,
            bool markdown)
        {
            switch (grouping)
            {
                case "tests":
                    FilterToTestName();
                    await GroupByTests();
                    break;
                case "builds":
                    FilterToTestName();
                    GroupByBuilds();
                    break;
                case "jobs":
                    FilterToTestName();
                    GroupByJobs();
                    break;
                default:
                    throw new Exception($"{grouping} is not a valid grouping");
            }

            void FilterToTestName()
            {
                if (!string.IsNullOrEmpty(name))
                {
                    var regex = new Regex(name, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    collection = collection.FilterToTestCaseTitle(regex);
                }
            }

            void GroupByBuilds()
            {
                foreach (var buildTestInfo in collection)
                {
                    PrintFailedTests(buildTestInfo);
                }
            }

            async Task GroupByTests()
            {
                if (markdown)
                {
                    await GroupByTestsMarkdown(collection);
                }
                else
                {
                    await GroupByTestsConsole(collection);
                }
            }

            async Task GroupByTestsConsole(BuildTestInfoCollection collection)
            {
                var all = collection
                    .GetTestCaseTitles()
                    .Select(t => (TestCaseTitle: t, Results: collection.GetDotNetTestCaseResultForTestCaseTitle(t)))
                    .OrderByDescending(t => t.Results.Count);

                foreach (var (testCaseTitle, testRunList) in all)
                {
                    Console.WriteLine($"{testCaseTitle} ({testRunList.Count})");
                    if (verbose)
                    {
                        Console.WriteLine($"{GetIndent(1)}Builds");
                        foreach (var build in collection.GetBuildsForTestCaseTitle(testCaseTitle))
                        {
                            var uri = DevOpsUtil.GetBuildUri(build);
                            Console.WriteLine($"{GetIndent(2)}{uri}");
                        }

                        Console.WriteLine($"{GetIndent(1)}Helix Logs");
                        foreach (var (_, helixLogInfo) in await GetHelixLogs(collection, testCaseTitle))
                        {
                            Console.WriteLine($"{GetIndent(2)} run_client.py {GetUri(helixLogInfo.RunClientUri)}");
                            Console.WriteLine($"{GetIndent(2)} console       {GetUri(helixLogInfo.ConsoleUri)}");
                            Console.WriteLine($"{GetIndent(2)} core          {GetUri(helixLogInfo.CoreDumpUri)}");
                            Console.WriteLine($"{GetIndent(2)} test results  {GetUri(helixLogInfo.TestResultsUri)}");
                        }

                        string GetUri(string? uri) => uri ?? "null";
                    }
                }
            }

            async Task GroupByTestsMarkdown(BuildTestInfoCollection collection)
            {
                foreach (var testCaseTitle in collection.GetTestCaseTitles())
                {
                    Console.WriteLine($"## {testCaseTitle}");
                    Console.WriteLine("");
                    Console.WriteLine("### Console Log Summary");
                    Console.WriteLine("");
                    Console.WriteLine("### Builds");
                    Console.WriteLine("|Build|Pull Request | Test Failure Count|");
                    Console.WriteLine("| --- | --- | --- |");
                    foreach (var buildTestInfo in collection.GetBuildTestInfosForTestCaseTitle(testCaseTitle))
                    {
                        var build = buildTestInfo.Build;
                        var uri = DevOpsUtil.GetBuildUri(build);
                        var pr = GetPullRequestColumn(build);
                        var testFailureCount = buildTestInfo.GetDotNetTestCaseResultForTestCaseTitle(testCaseTitle).Count();
                        Console.WriteLine($"|[#{build.Id}]({uri})|{pr}|{testFailureCount}|");
                    }

                    Console.WriteLine($"### Configurations");
                    foreach (var testRunName in collection.GetTestRunNamesForTestCaseTitle(testCaseTitle))
                    {
                        Console.WriteLine($"- {EscapeAtSign(testRunName)}");
                    }

                    Console.WriteLine($"### Helix Logs");
                    Console.WriteLine("|Build|Pull Request|Console|Core|Test Results|Run Client|");
                    Console.WriteLine("| --- | --- | --- | --- | --- | --- |");
                    foreach (var (build, helixLogInfo) in await GetHelixLogs(collection, testCaseTitle))
                    {
                        var uri = DevOpsUtil.GetBuildUri(build);
                        var pr = GetPullRequestColumn(build);
                        Console.Write($"|[#{build.Id}]({uri})|{pr}");
                        PrintUri(helixLogInfo.ConsoleUri, "console.log");
                        PrintUri(helixLogInfo.CoreDumpUri, "core");
                        PrintUri(helixLogInfo.TestResultsUri, "testResults.xml");
                        PrintUri(helixLogInfo.RunClientUri, "run_client.py");
                        Console.WriteLine("|");
                    }

                    static void PrintUri(string? uri, string displayName)
                    {
                        if (uri is null)
                        {
                            Console.Write("|");
                            return;
                        }

                        Console.Write($"|[{displayName}]({uri})");
                    }

                    static string EscapeAtSign(string text) => text.Replace("@", "@<!-- -->");

                    static string GetPullRequestColumn(Build build)
                    {
                        if (DevOpsUtil.TryGetPullRequestKey(build, out var pullRequestKey))
                        {
                            return $"#{pullRequestKey.Number}";
                        }

                        return "Rolling";
                    }

                    Console.WriteLine();
                }
            }

            void GroupByJobs()
            {
                var testRunNames = collection.GetTestRunNames();
                foreach (var testRunName in testRunNames)
                {
                    var list = collection.Where(x => x.ContainsTestRunName(testRunName));
                    Console.WriteLine($"{testRunName}");

                    if (verbose)
                    {
                        Console.WriteLine($"{GetIndent(1)}Builds");
                        foreach (var build in list)
                        {
                            var uri = DevOpsUtil.GetBuildUri(build.Build);
                            Console.WriteLine($"{GetIndent(2)}{uri}");
                        }
                    }

                    var testCaseIndent = 1;
                    if (verbose)
                    {
                        Console.WriteLine($"{GetIndent(1)}Test Cases");
                        testCaseIndent++;
                    }

                    var testCaseTitles = list
                        .SelectMany(x => x.GetDotNetTestCaseResultForTestRunName(testRunName))
                        .Select(x => x.TestCaseTitle)
                        .Distinct()
                        .OrderBy(x => x);
                    foreach (var testCaseTitle in testCaseTitles)
                    {
                        var count = list
                            .Count(x => x.DataList.Any(x => x.TestRunName == testRunName && x.TestCaseResults.Any(x => x.TestCaseTitle == testCaseTitle)));
                        Console.WriteLine($"{GetIndent(testCaseIndent)}{testCaseTitle} ({count})");
                    }
                }
            }
        }

        internal async Task<int> GetHelixPayload(IEnumerable<string> args)
        {
            var optionSet = new GetFromHelixOptionSet();
            ParseAll(optionSet, args);

            if (string.IsNullOrEmpty(optionSet.JobId))
            {
                return ReturnWithFailureMessage("JobId should not be empty");
            }

            if (string.IsNullOrEmpty(optionSet.DownloadDir))
            {
                return ReturnWithFailureMessage("Output directory should not be empty");
            }

            await HelixServer.GetHelixPayloads(optionSet.JobId, optionSet.WorkItems, optionSet.DownloadDir, optionSet.IgnoreDumps, resume:!optionSet.NoResume, extract:!optionSet.NoExtract).ConfigureAwait(false);

            int ReturnWithFailureMessage(string message)
            {
                Console.WriteLine(message);
                optionSet.WriteOptionDescriptions(Console.Out);
                return ExitFailure;
            }

            return ExitSuccess;
        }

        private static void PrintFailedTests(BuildTestInfo buildTestInfo)
        {
            var build = buildTestInfo.Build;
            Console.WriteLine($"{build.Id} {DevOpsUtil.GetBuildUri(build)}");
            foreach (var testRunName in buildTestInfo.GetTestRunNames())
            {
                Console.WriteLine($"{GetIndent(1)}{testRunName}");
                foreach (var testResult in buildTestInfo.GetDotNetTestCaseResultForTestRunName(testRunName))
                {
                    var suffix = "";
                    var testCaseResult = testResult.TestCaseResult;
                    if (testCaseResult.FailingSince is object &&
                        testCaseResult.FailingSince.Build.Id != build.Id)
                    {
                        suffix = $"(since {testCaseResult.FailingSince.Build.Id})";
                    }
                    Console.WriteLine($"{GetIndent(2)}{testCaseResult.TestCaseTitle} {suffix}");
                }
            }
        }

        private async Task<List<(Build, HelixLogInfo)>> GetHelixLogs(BuildTestInfoCollection collection, string testCaseTitle)
        {
            var query = collection
                .Select(x => (x.Build, TestCaseResults: x.DataList.SelectMany(x => x.TestCaseResults).Where(x => x.TestCaseTitle == testCaseTitle && x.IsHelixTestResult).ToList()))
                .Where(x => x.TestCaseResults.Count > 0)
                .SelectMany(x => x.TestCaseResults.Select(y => (x.Build, TestCaseResult: y)))
                .ToList()
                .AsParallel()
                .AsOrdered()
                .Select(async result =>
                {
                    var helixLogInfo = await GetHelixLogInfoAsync(result.TestCaseResult.HelixWorkItem!.Value);
                    return (result.Build, helixLogInfo);
                });
            var list = await RuntimeInfoUtil.ToListAsync(query);
            return list;
        }

        // The logs for the failure always exist on the associated work item, not on the 
        // individual test result
        private async Task<HelixLogInfo> GetHelixLogInfoAsync(HelixInfoWorkItem workItem) => await HelixUtil.GetHelixLogInfoAsync(HelixServer.GetHelixApi(), workItem);

        private static string GetIndent(int level) => level == 0 ? string.Empty : new string(' ', level * 2);

        private async Task<IEnumerable<Timeline>> ListTimelinesAsync(Build build, int? attempt)
        {
            var buildInfo = build.GetBuildResultInfo();
            switch (attempt)
            {
                case -1:
                    {
                        return await AzureUtil.ListTimelineAttemptsAsync(buildInfo.Project, buildInfo.Number);
                    }
                case null:
                    {
                        var single = await AzureUtil.GetTimelineAsync(buildInfo.Project, buildInfo.Number);
                        return new[] { single };
                    }
                case int attemptId when attemptId >= 1:
                    {
                        var single = await AzureUtil.GetTimelineAttemptAsync(buildInfo.Project, buildInfo.Number, attemptId);
                        return new[] { single };
                    }
                default:
                    throw new Exception($"Illegal attempt value {attempt}.");
            }
        }
    }
}