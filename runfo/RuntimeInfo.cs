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
                    Console.WriteLine($"Unable to download helix logs for {build.Id} {workItem.HelixInfo.JobId}");
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
        bool markdown = false;
        var optionSet = new BuildSearchOptionSet()
        {
            { "n|name=", "name regex to match in results", n => name = n },
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

        var found = await SearchTimelineIssues(Server, builds, nameRegex, textRegex);
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

        return ExitSuccess;

        async Task<List<(Build Build, TimelineRecord TimelineRecord, string Line)>> SearchTimelineIssues(
            DevOpsServer server,
            IEnumerable<Build> builds,
            Regex nameRegex,
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

                var records = timeline.Records.Where(r => nameRegex is null || nameRegex.IsMatch(r.Name));
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
        var optionSet = new BuildSearchOptionSet()
        {
            { "n|name=", "name regex to match in results", n => name = n },
            { "v|value=", "text to search for", t => text = t },
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

        var found = await SearchBuildLogs(Server, builds, nameRegex, textRegex);
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

        return ExitSuccess;

        static async Task<List<(Build Build, TimelineRecord TimelineRecord, string Line)>> SearchBuildLogs(
            DevOpsServer server,
            IEnumerable<Build> builds,
            Regex nameRegex,
            Regex textRegex)
        {
            // Deliberately iterating the build serially vs. using AsParallel because othewise we 
            // end up sending way too many requests to AzDO. Eventually it will begin rate limiting
            // us.
            var list = new List<(Build Build, TimelineRecord TimelineRecord, string Line)>();
            foreach (var build in builds)
            {
                var timeline = await server.GetTimelineAsync(build.Project.Name, build.Id);
                if (timeline is null)
                {
                    continue;
                }
                var records = timeline.Records.Where(r => nameRegex is null || nameRegex.IsMatch(r.Name));
                var all = (await SearchTimelineRecords(server, records, textRegex)).Select(x => (build, x.TimelineRecord, x.Line));
                list.AddRange(all);
            }

            return list;
        }

        static async Task<IEnumerable<(TimelineRecord TimelineRecord, string Line)>> SearchTimelineRecords(DevOpsServer server, IEnumerable<TimelineRecord> records, Regex textRegex)
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
            return (await RuntimeInfoUtil.ToList(all))
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

    internal async Task<int> PrintTimeline(IEnumerable<string> args)
    {
        int? depth = null;
        bool issues = false;
        bool failed = false;
        var optionSet = new BuildSearchOptionSet()
        {
            { "depth=", "depth to print to", (int d) => depth = d },
            { "issues", "print issues", i => issues = i is object },
            { "failed", "print records that failed", f => failed = f is object },
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

            DumpTimeline(Server, timeline, depth, issues, failed);
        }

        static void DumpTimeline(DevOpsServer server, Timeline timeline, int? depthLimit, bool issues, bool failed)
        {
            var records = timeline.Records;
            var map = new Dictionary<string, List<TimelineRecord>>();

            // Each stage will have a different root
            var roots = new List<TimelineRecord>();
            foreach (var record in records)
            {
                if (string.IsNullOrEmpty(record.ParentId))
                {
                    roots.Add(record);
                    continue;
                }

                if (!map.TryGetValue(record.ParentId, out var list))
                {
                    list = new List<TimelineRecord>();
                    map.Add(record.ParentId, list);
                }
                list.Add(record);
            }

            foreach (var root in roots.OrderBy(x => x.Order))
            {
                PrintRecord("Root", depth: 0, root);
                foreach (var topRecord in timeline.Records.Where(x => x.ParentId == root.Id).OrderBy(x => x.Name))
                {
                    DumpRecord(topRecord, 1);
                }
            }

            void DumpRecord(TimelineRecord current, int depth)
            {
                if (depth > depthLimit == true)
                {
                    return;
                }

                PrintRecord("Record", depth, current);
                foreach (var record in records.Where(x => x.ParentId == current.Id))
                {
                    if (issues && record.Issues is object)
                    {
                        var indent = GetIndent(depth + 1);
                        foreach (var issue in record.Issues)
                        {
                            Console.WriteLine($"{indent}{issue.Type} {issue.Category} {issue.Message}");
                        }
                    }

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

                    DumpRecord(record, depth + 1);
                }

            }

            void PrintRecord(string kind, int depth, TimelineRecord record)
            {
                if (failed && !(
                        record.Result == TaskResult.Failed ||
                        record.Result == TaskResult.Canceled ||
                        record.Result == TaskResult.Abandoned))
                {
                    return;
                }

                var indent = GetIndent(depth);
                var duration = RuntimeInfoUtil.TryGetDuration(record.StartTime, record.FinishTime);
                Console.WriteLine($"{indent}{kind} {record.Name} ({duration}) {record.Result}");
            }

            string GetIndent(int depth) => new string(' ', depth * 2);
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

        Console.WriteLine($"Definition Build    Result       Url");
        foreach (var build in builds)
        {
            Console.WriteLine($"{build.Definition.Id,-10} {build.Id,-8} {build.Result,-12} {DevOpsUtil.GetBuildUri(build)}");
        }

        return ExitSuccess;
    }

    internal async Task<int> PrintFailedTests(IEnumerable<string> args)
    {
        bool verbose = false;
        bool markdown = false;
        string name = null;
        string grouping = "builds";
        var optionSet = new BuildSearchOptionSet()
        {
            { "g|grouping=", "output grouping: builds*, tests, jobs", g => grouping = g },
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
                GroupByTestsConsole(collection);
            }
        }

        void GroupByTestsConsole(BuildTestInfoCollection collection)
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
                Console.WriteLine("|Build|Pull Request|Console|Core|Test Results|");
                Console.WriteLine("| --- | --- | --- | --- | --- |");
                foreach (var (build, helixLogInfo) in await GetHelixLogs(collection, testCaseTitle))
                {
                    var uri = DevOpsUtil.GetBuildUri(build);
                    var pr = GetPullRequestColumn(build);
                    Console.Write($"|[#{build.Id}]({uri})|{pr}");
                    PrintUri(helixLogInfo.ConsoleUri, "console");
                    PrintUri(helixLogInfo.CoreDumpUri, "core");
                    PrintUri(helixLogInfo.TestResultsUri, "testResults.xml");
                    Console.WriteLine("|");
                }

                static void PrintUri(string uri, string defaultDisplayName)
                {
                    if (uri is null)
                    {
                        Console.Write("|");
                        return;
                    }

                    try
                    {
                        if (Uri.TryCreate(uri, UriKind.Absolute, out var realUri))
                        {
                            var name = Path.GetFileName(realUri.LocalPath);
                            Console.Write($"|[{name}]({uri})");
                            return;
                        }
                    }
                    catch
                    {
                        // Badly formatted URI
                    }

                    Console.Write($"|[{defaultDisplayName}]({uri})");
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
                if (testCaseResult.FailingSince.Build.Id != build.Id)
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
        var before = optionSet.Before;
        var after = optionSet.After;

        var builds = new List<Build>();
        if (optionSet.BuildIds.Count > 0)
        {
            if (optionSet.Repository is object)
            {
                OptionFailure("Cannot specify builds and repository", optionSet);
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
                includePullRequests: optionSet.IncludePullRequests);
            builds.AddRange(collection);
        }

        if (before.HasValue)
        {
            builds = builds.Where(b => b.GetStartTime() is DateTime d && d <= before.Value).ToList();
        }

        if (after.HasValue)
        {
            builds = builds.Where(b => b.GetStartTime() is DateTime d && d >= after.Value).ToList();
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

    private async Task<List<Build>> ListBuildsAsync(string project, int count, int[] definitions = null, string repositoryId = null, bool includePullRequests = false)
    {
        var list = new List<Build>();
        var builds = Server.EnumerateBuildsAsync(
            project,
            definitions: definitions,
            repositoryId: repositoryId,
            statusFilter: BuildStatus.Completed,
            queryOrder: BuildQueryOrder.FinishTimeDescending);
        await foreach (var build in builds)
        {
            if (build.Reason == BuildReason.PullRequest && !includePullRequests)
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