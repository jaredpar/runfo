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
using static RuntimeInfoUtil;

internal sealed class RuntimeInfo
{
    internal static readonly (string BuildName, string Project, int DefinitionId)[] BuildDefinitions = new[]
        {
            ("runtime", "public", 686),
            ("runtime-official", "internal", 679),
            ("coreclr", "public", 655),
            ("libraries", "public", 675),
            ("libraries windows", "public", 676),
            ("libraries linux", "public", 677),
            ("libraries osx", "public", 678),
            ("crossgen2", "public", 701),
            ("roslyn", "public", 15),
            ("roslyn-integration", "public", 245),
            ("aspnet", "public", 278),
            ("sdk", "public", 136),
            ("winforms", "public", 267),
        };

    internal DevOpsServer Server;

    internal RuntimeInfo(string personalAccessToken = null, bool cacheable = false)
    {
        Server = cacheable
            ? new CachingDevOpsServer(RuntimeInfoUtil.CacheDirectory, "dnceng", personalAccessToken)
            : new DevOpsServer("dnceng", personalAccessToken);
    }

    internal async Task PrintBuildResults(IEnumerable<string> args)
    {
        int count = 5;
        var optionSet = new OptionSet()
        {
            { "c|count=", "count of builds to return", (int c) => count = c }
        };

        ParseAll(optionSet, args);

        var data = BuildDefinitions
            .AsParallel()
            .AsOrdered()
            .Select(async t => (t.BuildName, t.DefinitionId, await ListBuildsAsync(t.Project, count, new[] { t.DefinitionId })));

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
                catch(Exception ex)
                {
                    Console.WriteLine($"Error deleting {filePath}: {ex.Message}");
                }
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Cannot clear cache: {ex.Message}");
        }
    }

    internal async Task<int> PrintSearchHelix(IEnumerable<string> args)
    {
        string text = null;
        var optionSet = new BuildSearchOptionSet()
        {
            { "v|value=", "text to search for", t => text = t },
        };

        ParseAll(optionSet, args);

        if (text is null)
        {
            Console.WriteLine("Must provide a text argument to search for");
            optionSet.WriteOptionDescriptions(Console.Out);
            return ExitFailure;
        }

        var badLogList = new List<string>();
        var textRegex = new Regex(text, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var collection = await ListBuildTestInfosAsync(optionSet);
        var found = collection
            .AsParallel()
            .Select(async b => (b.Build, await SearchBuild(b)));

        Console.WriteLine("|Build|Kind|Console Log|");
        Console.WriteLine("|---|---|---|");
        foreach (var task in found)
        {
            var (build, helixLogInfo) = await task;
            if (helixLogInfo is null)
            {
                continue;
            }

            var kind = "Rolling";
            if (DevOpsUtil.GetPullRequestNumber(build) is int pr)
            {
                kind = $"PR https://github.com/dotnet/runtime/pull/{pr}";
            }
            Console.WriteLine($"|[{build.Id}]({DevOpsUtil.GetBuildUri(build)})|{kind}|[console.log]({helixLogInfo.ConsoleUri})|");
        }

        foreach (var line in badLogList)
        {
            Console.WriteLine(line);
        }

        return ExitSuccess;

        async Task<HelixLogInfo> SearchBuild(BuildTestInfo buildTestInfo)
        {
            var build = buildTestInfo.Build;
            foreach (var workItem in buildTestInfo.GetHelixWorkItems())
            {
                try
                {
                    var logInfo = await GetHelixLogInfoAsync(workItem);
                    if (logInfo.ConsoleUri is object)
                    {
                        using var stream = await Server.DownloadFileAsync(logInfo.ConsoleUri);
                        if (IsMatch(stream, textRegex))
                        {
                            return logInfo;
                        }
                    }
                }
                catch
                {
                    badLogList.Add($"Unable to download helix logs for {build.Id} {workItem.HelixInfo.JobId}");
                }
            }

            return null;
        }

        static bool IsMatch(Stream stream, Regex textRegex)
        {
            using var reader = new StreamReader(stream);
            do
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                if (textRegex.IsMatch(line))
                {
                    return true;
                }

            } while (true);

            return false;
        }
    }

    internal async Task<int> PrintSearchTimeline(IEnumerable<string> args)
    {
        string name = null;
        string text = null;
        string task = null;
        bool markdown = false;
        var optionSet = new BuildSearchOptionSet()
        {
            { "n|name=", "Search only records matching this name", n => name = n },
            { "t|task=", "Search only tasks matching this name", t => task = t },
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

        var hadDefinition = optionSet.Definitions.Any();
        var builds = await ListBuildsAsync(optionSet);
        var textRegex = new Regex(text, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex nameRegex = null;
        if (name is object)
        {
            nameRegex = new Regex(name, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        Regex taskRegex = null;
        if (task is object)
        {
            taskRegex = new Regex(task, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        if (markdown)
        {
            if (!hadDefinition)
            {
                Console.Write("|Definition");
            }

            Console.WriteLine("|Build|Kind|Timeline Record|");

            if (!hadDefinition)
            {
                Console.Write("|---");
            }

            Console.WriteLine("|---|---|---|");
        }

        var found = await SearchTimelineIssues(Server, builds, nameRegex, taskRegex, textRegex);
        foreach (var tuple in found)
        {
            var build = tuple.Build;
            if (markdown)
            {
                if (!hadDefinition)
                {
                    var definitionName = GetDefinitionName(build);
                    var definitionUri = DevOpsUtil.GetBuildDefinitionUri(build);
                    Console.Write($"|[{definitionName}]({definitionUri})");
                }

                var kind = "Rolling";
                if (DevOpsUtil.GetPullRequestNumber(build) is int pr)
                {
                    kind = $"PR https://github.com/{build.Repository.Id}/pull/{pr}";
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
        Console.WriteLine($"Impacted {foundBuildCount} bulids");
        Console.WriteLine($"Impacted {found.Count} jobs");

        return ExitSuccess;

        async Task<List<(Build Build, TimelineRecord TimelineRecord, string Line)>> SearchTimelineIssues(
            DevOpsServer server,
            IEnumerable<Build> builds,
            Regex nameRegex,
            Regex taskRegex,
            Regex textRegex)
        {
            var list = new List<(Build Build, TimelineRecord TimelineRecord, string Line)>();
            foreach (var build in builds)
            {
                var timeline = await server.GetTimelineAsync(build.Project.Name, build.Id);
                if (timeline is null)
                {
                    continue;
                }

                var records = timeline.Records
                    .Where(r => nameRegex is null || nameRegex.IsMatch(r.Name))
                    .Where(r => r.Task is null || taskRegex is null || taskRegex.IsMatch(r.Task.Name));
                foreach (var record in records)
                {
                    if (record.Issues is null)
                    {
                        continue;
                    }

                    string line = null;
                    foreach (var issue in record.Issues)
                    {
                        if (textRegex.IsMatch(issue.Message))
                        {
                            line = issue.Message;
                            break;
                        }
                    }

                    if (line is object)
                    {
                        list.Add((build, record, line));
                    }
                }
            }

            return list;
        }

    }

    internal async Task<int> PrintSearchBuildLogs(IEnumerable<string> args)
    {
        string name = null;
        string text = null;
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

        var builds = await ListBuildsAsync(optionSet);
        var textRegex = new Regex(text, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex nameRegex = null;
        if (name is object)
        {
            nameRegex = new Regex(name, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        if (markdown)
        {
            Console.WriteLine("|Build|Kind|Timeline Record|");
            Console.WriteLine("|---|---|---|");
        }

        var (found, recordCount) = await SearchBuildLogs(Server, builds, nameRegex, textRegex, trace);
        foreach (var tuple in found)
        {
            var build = tuple.Build;
            if (markdown)
            {
                var kind = "Rolling";
                if (DevOpsUtil.GetPullRequestNumber(build) is int pr)
                {
                    kind = $"PR https://github.com/{build.Repository.Id}/pull/{pr}";
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
        Console.WriteLine($"Impacted {foundBuildCount} bulids");
        Console.WriteLine($"Impacted {found.Count} jobs");
        Console.WriteLine($"Searched {recordCount} records");

        return ExitSuccess;

        static async Task<(List<(Build Build, TimelineRecord TimelineRecord, string Line)>, int Count)> SearchBuildLogs(
            DevOpsServer server,
            IEnumerable<Build> builds,
            Regex nameRegex,
            Regex textRegex,
            bool trace)
        {
            // Deliberately iterating the build serially vs. using AsParallel because othewise we 
            // end up sending way too many requests to AzDO. Eventually it will begin rate limiting
            // us.
            var list = new List<(Build Build, TimelineRecord TimelineRecord, string Line)>();
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

        static async Task<IEnumerable<(TimelineRecord TimelineRecord, string Line)>> SearchTimelineRecords(
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

            var list = await RuntimeInfoUtil.ToList(all);
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

        static async Task<(bool IsMatch, string Line)> SearchTimelineRecord(DevOpsServer server, TimelineRecord record, Regex textRegex)
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

    internal async Task<int> PrintHelix(IEnumerable<string> args)
    {
        var verbose = false;
        var optionSet = new BuildSearchOptionSet()
        {
            { "v|verbose", "verbose output", v => verbose = v is object },
        };

        ParseAll(optionSet, args);

        foreach (var buildTestInfo in await ListBuildTestInfosAsync(optionSet))
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

    private async Task<List<(HelixLogInfo HelixLogInfo, string ConsoleText)>> GetHelixLogsAsync(BuildTestInfo buildTestInfo, bool includeConsoleText)
    {
        var logs = buildTestInfo
            .GetHelixWorkItems()
            .AsParallel()
            .Select(async t => await GetHelixLogInfoAsync(t))
            .Select(async (Task<HelixLogInfo> task) =>
            {
                var helixLogInfo = await task;
                string consoleText = null;
                if (includeConsoleText && helixLogInfo.ConsoleUri is object)
                {
                    consoleText = await HelixUtil.GetHelixConsoleText(Server, helixLogInfo.ConsoleUri);
                }
                return (helixLogInfo, consoleText);
            });

        return await RuntimeInfoUtil.ToList(logs);
    }

    internal void PrintBuildDefinitions()
    {
        foreach (var (name, project, definitionId) in BuildDefinitions)
        {
            var uri = DevOpsUtil.GetBuildDefinitionUri(Server.Organization, project, definitionId);
            Console.WriteLine($"{name,-20}{uri}");
        }
    }

    internal async Task<int> PrintBuilds(IEnumerable<string> args)
    {
        var optionSet = new BuildSearchOptionSet();
        ParseAll(optionSet, args);

        foreach (var build in await ListBuildsAsync(optionSet))
        {
            var uri = DevOpsUtil.GetBuildUri(build);
            var prId = DevOpsUtil.GetPullRequestNumber(build);
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
            foreach (var build in await ListBuildsAsync(optionSet))
            {
                var artifacts = await Server.ListArtifactsAsync(build.Project.Name, build.Id);
                list.Add((build, artifacts));
            }

            return list;
        }

        (double Total, double Average, double Max) GetArtifactInfo(List<BuildArtifact> artifacts)
        {
            var sizes = artifacts
                .Select(x => x.GetByteSize() is int i ? (double?)i : null)
                .Where(x => x.HasValue)
                .Select(x => x.Value / mb);
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
                    .Select(x => (Artifact: x, Size: (double)x.GetByteSize() / mb))
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
        string name = null;
        var optionSet = new BuildSearchOptionSet()
        {
            { "depth=", "depth to print to", (int d) => depth = d },
            { "issues", "print recrods that have issues", i => issues = i is object },
            { "failed", "print records that failed", f => failed = f is object },
            { "n|name=", "record name to search for", n => name = n},
            { "v|verbose", "print issues with records", v => verbose = v is object },
        };

        ParseAll(optionSet, args);

        foreach (var build in await ListBuildsAsync(optionSet))
        {
            Console.WriteLine(DevOpsUtil.GetBuildUri(build));
            var timeline = await Server.GetTimelineAsync(build.Project.Name, build.Id);
            if (timeline is null)
            {
                Console.WriteLine("No timeline info");
                return ExitFailure;
            }

            DumpTimeline(Server, timeline, depth, issues, failed, verbose);
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
                tree = tree.Filter(x => x.Result == TaskResult.Failed);
            }

            foreach (var root in tree.Roots)
            {
                DumpRecord(root, 0);
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

    internal async Task<int> PrintBuildYaml(IEnumerable<string> args)
    {
        var optionSet = new BuildSearchOptionSet();
       ParseAll(optionSet, args);

        foreach (var build in await ListBuildsAsync(optionSet))
        {
            var log = await Server.GetBuildLogAsync(build.Project.Name, build.Id, logId: 1);
            Console.WriteLine(log);
        }

        return ExitSuccess;
    }

    internal async Task<int> PrintPullRequestBuilds(IEnumerable<string> args)
    {
        string repository = null;
        int? number = null;
        string definition = null;
        var optionSet = new OptionSet()
        {
            { "d|definition=", "definition to print tests for", d => definition = d },
            { "r|repository=", "repository name (dotnet/runtime)", r => repository = r },
            { "n|number=", "pull request number", (int n) => number = n },
        };

        ParseAll(optionSet, args);

        var project = BuildSearchOptionSet.DefaultProject;
        IEnumerable<int> definitions = null;
        if (definition is object)
        {
            if (!TryGetDefinitionId(definition, out string definitionProject, out int definitionId))
            {
                OptionFailureDefinition(definition, optionSet);
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

        IEnumerable<Build> builds = await Server.ListBuildsAsync(
            project,
            definitions: definitions,
            repositoryId: repository,
            branchName: $"refs/pull/{number.Value}/merge",
            repositoryType: "github");
        builds = builds.OrderByDescending(b => b.Id);

        Console.WriteLine($"Definition           Build    Result       Url");
        foreach (var build in builds)
        {
            var name = GetDefinitionName(build);
            Console.WriteLine($"{name,-20} {build.Id,-8} {build.Result,-12} {DevOpsUtil.GetBuildUri(build)}");
        }

        return ExitSuccess;
    }

    internal async Task<int> PrintFailedTests(IEnumerable<string> args)
    {
        bool verbose = false;
        bool markdown = false;
        string name = null;
        string grouping = "tests";
        var optionSet = new BuildSearchOptionSet()
        {
            { "g|grouping=", "output grouping: tests*, builds, jobs", g => grouping = g },
            { "m|markdown", "output in markdown", m => markdown = m  is object },
            { "n|name=", "name regex to match in results", n => name = n },
            { "v|verbose", "verobes output", d => verbose = d is object },
        };

        ParseAll(optionSet, args);

        var collection = await ListBuildTestInfosAsync(optionSet);
        await PrintFailureInfo(collection, grouping, name, verbose, markdown);
        return ExitSuccess;
    }

    private async Task PrintFailureInfo(
        BuildTestInfoCollection collection,
        string grouping,
        string name,
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
                Console.WriteLine($"{testCaseTitle} {testRunList.Count}");
                if (verbose)
                {
                    Console.WriteLine($"{GetIndent(1)}Builds");
                    foreach (var build in collection.GetBuildsForTestCaseTitle(testCaseTitle))
                    {
                        var uri = DevOpsUtil.GetBuildUri(build);
                        Console.WriteLine($"{GetIndent(2)}{uri}");
                    }

                    Console.WriteLine($"{GetIndent(1)}Test Runs");
                    foreach (var helixTestRunResult in testRunList)
                    {
                        var testRun = helixTestRunResult.TestRun;
                        var count = testRunList.Count(t => t.TestRun.Name == testRun.Name);
                        Console.WriteLine($"{GetIndent(2)}{count}\t{testRun.Name}");
                    }

                    Console.WriteLine($"{GetIndent(1)}Helix Logs");
                    foreach (var (build, helixLogInfo) in await GetHelixLogs(collection, testCaseTitle))
                    {
                        Console.WriteLine($"{GetIndent(2)} run_client.py {GetUri(helixLogInfo.RunClientUri)}");
                        Console.WriteLine($"{GetIndent(2)} console       {GetUri(helixLogInfo.ConsoleUri)}");
                        Console.WriteLine($"{GetIndent(2)} core          {GetUri(helixLogInfo.CoreDumpUri)}");
                        Console.WriteLine($"{GetIndent(2)} test results  {GetUri(helixLogInfo.TestResultsUri)}");
                    }

                    string GetUri(string uri) => uri ?? "null";
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

                static void PrintUri(string uri, string displayName)
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
                    var prNumber = DevOpsUtil.GetPullRequestNumber(build);
                    if (prNumber is null)
                    {
                        return "Rolling";
                    }

                    return $"#{prNumber.Value}";
                }

                Console.WriteLine();
            }
        }

        void GroupByJobs()
        {
            if (!string.IsNullOrEmpty(name))
            {
                var regex = new Regex(name, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                collection = collection.FilterToTestRunName(regex);
            }

            var testRunNames = collection.GetTestRunNames();
            foreach (var testRunName in testRunNames)
            {
                var list = collection.Where(x => x.ContainsTestRunName(testRunName));
                if (verbose)
                {
                    Console.WriteLine($"{testRunName}");
                    Console.WriteLine($"{GetIndent(1)}Builds");
                    foreach (var build in list)
                    {
                        var uri = DevOpsUtil.GetBuildUri(build.Build);
                        Console.WriteLine($"{GetIndent(2)}{uri}");
                    }

                    Console.WriteLine($"{GetIndent(1)}Test Cases");
                    var testCaseTitles = list
                        .SelectMany(x => x.GetDotNetTestCaseResultForTestRunName(testRunName))
                        .Select(x => x.TestCaseTitle)
                        .Distinct()
                        .OrderBy(x => x);
                    foreach (var testCaseTitle in testCaseTitles)
                    {
                        var count = list
                            .SelectMany(x => x.GetDotNetTestCaseResultForTestCaseTitle(testCaseTitle))
                            .Count(x => x.TestRun.Name == testRunName);
                        Console.WriteLine($"{GetIndent(2)}{testCaseTitle} ({count})");
                    }
                }
                else
                {
                    var buildCount = list.Count();
                    var testCaseCount = list.Sum(x => x.GetDotNetTestCaseResultForTestRunName(testRunName).Count());
                    Console.WriteLine($"{testRunName} Builds {buildCount} Tests {testCaseCount}");
                }
            }
        }
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
            .GetDotNetTestCaseResultForTestCaseTitle(testCaseTitle)
            .Where(x => x.HelixWorkItem.HasValue)
            .OrderBy(x => x.Build.Id)
            .ToList()
            .AsParallel()
            .AsOrdered()
            .Select(async result =>
            {
                var helixLogInfo = await GetHelixLogInfoAsync(result.HelixWorkItem.Value);
                return (result.Build, helixLogInfo);
            });
        var list = await RuntimeInfoUtil.ToList(query);
        return list;
    }

    private async Task<BuildTestInfo> GetBuildTestInfoAsync(string project, int buildId)
    {
        var build = await Server.GetBuildAsync(project, buildId);
        return await GetBuildTestInfoAsync(build);
    }

    private async Task<BuildTestInfo> GetBuildTestInfoAsync(Build build)
    {
        var collection = await DotNetUtil.ListDotNetTestRunsAsync(Server, build, TestOutcome.Failed);
        return new BuildTestInfo(build, collection.SelectMany(x => x.TestCaseResults).ToList());
    }

    private string GetDefinitionName(Build build) => 
        TryGetDefinitionName(build, out var name) 
            ? name
            : build.Definition.Name.ToString();

    private bool TryGetDefinitionName(Build build, out string name)
    {
        var project = build.Project.Name;
        var id = build.Definition.Id;
        foreach (var tuple in BuildDefinitions)
        {
            if (tuple.Project == project && tuple.DefinitionId == id)
            {
                name = tuple.BuildName;
                return true;
            }
        }

        name = null;
        return false;
    }

    private bool TryGetDefinitionId(string definition, out string project, out int definitionId)
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
            project = both[1];
        }

        if (int.TryParse(definition, out definitionId))
        {
            return true;
        }

        foreach (var (name, p, id) in BuildDefinitions)
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

    private static void OptionFailure(string message, OptionSet optionSet)
    {
        Console.WriteLine(message);
        optionSet.WriteOptionDescriptions(Console.Out);
    }

    private static void OptionFailureDefinition(string definition, OptionSet optionSet)
    {
        Console.WriteLine($"{definition} is not a valid definition name or id");
        Console.WriteLine("Supported definition names");
        foreach (var (name, _, id) in BuildDefinitions)
        {
            Console.WriteLine($"{id}\t{name}");
        }

        optionSet.WriteOptionDescriptions(Console.Out);
    }

    // The logs for the failure always exist on the associated work item, not on the 
    // individual test result
    private async Task<HelixLogInfo> GetHelixLogInfoAsync(HelixWorkItem workItem) => await HelixUtil.GetHelixLogInfoAsync(Server, workItem);

    private static string GetIndent(int level) => level == 0 ? string.Empty : new string(' ', level * 2);

    private async Task<List<Build>> ListBuildsAsync(BuildSearchOptionSet optionSet)
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
                if (!TryGetBuildId(buildInfo, out var buildProject, out var buildId))
                {
                    OptionFailure($"Cannot convert {buildInfo} to build id", optionSet);
                    throw CreateBadOptionException();
                }

                buildProject ??= project;
                var build = await Server.GetBuildAsync(buildProject, buildId);
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

        static Exception CreateBadOptionException() => new Exception("Bad option");

        static bool TryGetBuildId(string build, out string project, out int buildId)
        {
            project = null;

            var index = build.IndexOf(':');
            if (index >= 0)
            {
                var both = build.Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
                build = both[0];
                project = both[1];
            }

            return int.TryParse(build, out buildId);
        }
    }

    private async Task<List<Build>> ListBuildsAsync(
        string project,
        int count,
        int[] definitions = null,
        string repositoryId = null,
        string branchName = null,
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

    private async Task<BuildTestInfoCollection> ListBuildTestInfosAsync(BuildSearchOptionSet optionSet)
    {
        var list = new List<BuildTestInfo>();
        foreach (var build in await ListBuildsAsync(optionSet))
        {
            try
            {
                list.Add(await GetBuildTestInfoAsync(build));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cannot get test info for {build.Id} {DevOpsUtil.GetBuildUri(build)}");
                Console.WriteLine(ex.Message);
            }
        }

        return new BuildTestInfoCollection(new ReadOnlyCollection<BuildTestInfo>(list));
    }

}