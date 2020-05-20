using DevOps.Util;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices.ComTypes;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using DevOps.Util.DotNet;
using System.Net;
using System.ComponentModel.DataAnnotations;
using Octokit;
using System.Dynamic;
using System.Net.Http;
using System.Text;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Core;

namespace QueryFun
{
    public class Program
    {
        public static string Organization = "dnceng";

        public static async Task Main(string[] args)
        {
            await Scratch();
            // await ListStaleChecks();
            // await ListBuildsFullAsync();
            // await UploadCloneTime();
            // await DumpCheckoutTimes("dnceng", "public", 196, top: 200);
            // Roslyn
            // await DumpCheckoutTimes("dnceng", "public", 15, top: 200);
            // Roslyn Integration
            // await DumpCheckoutTimes("dnceng", "public", 245, top: 200);
            // CoreFx
            // await DumpCheckoutTimes("dnceng", "public", 196, top: 200);
            // CoreClr
            // await DumpCheckoutTimes("dnceng", "public", 228, top: 200);
            // CLI
            // await DumpCheckoutTimes("dnceng", "public", 166, top: 200);
            // ASP.NET
            // await DumpCheckoutTimes("dnceng", "public", 278, top: 200);
            // await DumpTimelines("dnceng", "public", 15, top: 20);
            // await DumpTestTimes();
            // await UploadNgenData();
            // await DumpNgenData();
            // await DumpNgenData(2916584;
            // await Fun();
            // await DumpTimeline("public", 196140);
        }

        private static async Task<string> GetToken(string name)
        {
            var lines = await File.ReadAllLinesAsync(@"p:\tokens.txt");
            foreach (var line in lines)
            {
                var split = line.Split(':', count: 2);
                if (name == split[0])
                {
                    return split[1];
                }
            }

            throw new Exception($"Could not find token with name {name}");

        }

        private static async Task Scratch()
        {
            // await DumpMachineUsage();
            // await DumpJobFailures();
            await DumpTimelines();
        }

        public static async Task DumpTimelines()
        {
            var server = new DevOpsServer("dnceng", Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN"));
            var queryUtil = new DotNetQueryUtil(server);

            foreach (var build in await queryUtil.ListBuildsAsync("-d runtime -c 30 -pr"))
            {
                var timeline = await server.GetTimelineAsync(build.Project.Name, build.Id);
                if (timeline is null)
                {
                    continue;
                }

                for (var i = timeline.GetAttempt(); i >= 1; i--)
                {
                    var t = await server.GetTimelineAttemptAsync(build.Project.Name, build.Id, i);
                    var tree = TimelineTree.Create(t);
                    Console.WriteLine($"{build.Id} {i}");
                }
            }
        }

        public static async Task DumpJobFailures()
        {
            var server = new DevOpsServer("dnceng", Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN"));
            var queryUtil = new DotNetQueryUtil(server);
            var jobCount = 0;
            var testFailCount = 0;
            var otherFailCount = 0;

            foreach (var build in await queryUtil.ListBuildsAsync("-d runtime -c 100"))
            {
                try
                {
                    await HandleBuild(build);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error with {build.Id}: {ex.Message}");
                }
            }

            int totalFail = testFailCount + otherFailCount;
            Console.WriteLine($"Total Jobs: {jobCount}");
            Console.WriteLine($"Failed Tests: {testFailCount} {GetPercent(testFailCount)}");
            Console.WriteLine($"Failed Other: {otherFailCount} {GetPercent(otherFailCount)}");

            string GetPercent(int count)
            {
                var p = (double)count / totalFail;
                p *= 100;
                return $"({p:N2}%)";
            }

            async Task HandleBuild(Build build)
            {
                var timeline = await server.GetTimelineAttemptAsync(build.Project.Name, build.Id, attempt: 1);
                var tree = TimelineTree.Create(timeline);
                var helixJobs = await queryUtil.ListHelixJobsAsync(timeline);
                foreach (var job in tree.Jobs)
                {
                    jobCount++;
                    if (!job.IsAnyFailed())
                    {
                        continue;
                    }

                    if (helixJobs.Any(x => x.JobName == job.Name))
                    {
                        testFailCount++;
                    }
                    else
                    {
                        otherFailCount++;
                    }
                }
            }

        }

        private static async Task DumpDownloadTimes()
        {
            var server = new DevOpsServer("dnceng", Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN"));
            var queryUtil = new DotNetQueryUtil(server);

            Console.WriteLine("Build Uri,Pull Request,Minutes");
            foreach (var build in await queryUtil.ListBuildsAsync("-d runtime -c 100 -pr"))
            {
                try
                {
                    var timeline = await server.GetTimelineAsync(build.Project.Name, build.Id);
                    var tree = TimelineTree.Create(timeline);
                    var node = tree
                        .Nodes
                        .Where(x => 
                            x.Name == "Download artifacts for all platforms" &&
                            tree.TryGetJob(x.TimelineRecord, out var job) &&
                            job.Name == "Installer Build and Test mono iOS_arm Release")
                        .FirstOrDefault();
                    if (node is object &&
                        node.TimelineRecord.GetStartTime() is DateTimeOffset start &&
                        node.TimelineRecord.GetFinishTime() is DateTimeOffset finish)
                    {
                        var buildInfo = build.GetBuildInfo();
                        var isPr = buildInfo.PullRequestKey.HasValue;
                        Console.WriteLine(buildInfo.BuildUri + $",{isPr}," + ((int)((finish - start).TotalMinutes)).ToString());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error {build.Id}: {ex.Message}");
                }
            }

        }



        private class BuildStats
        {
            public int Passed { get; set; }
            public int PassedOnRetry { get; set;}
            public int Failed { get; set; }


            public int TotalPassed => Passed + PassedOnRetry;

            public int Total => Passed + PassedOnRetry + Failed;

            public double PercentPassed => (double)TotalPassed / Total;
        }

        private class DayBuildStats
        {
            public BuildStats MergedPullRequests { get; set; } = new BuildStats();
            public BuildStats CIBuilds { get; set; } = new BuildStats();
        }

        public static async Task DumpTestTotals()
        {
            var project = "public";
            var buildId = 642971;
            var server = new DevOpsServer("dnceng", Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN"));
            var queryUtil = new DotNetQueryUtil(server);
            var build = await server.GetBuildAsync(project, buildId);
            var testRuns = await queryUtil.ListDotNetTestRunsAsync(build);
            var testCases = testRuns.SelectMany(x => x.TestCaseResults).ToList();
            Console.WriteLine($"Total {testCases.Count}");

            var uniqueCount = testCases.GroupBy(x => x.TestCaseTitle).Count();
            Console.WriteLine($"Unique {uniqueCount}");
            var query = testCases
                .GroupBy(x => x.TestCaseTitle)
                .OrderByDescending(x => x.Count())
                .Take(10);
            foreach (var item in query)
            {
                Console.WriteLine($"{item.Key} {item.Count()}");
            }
        }

        private static async Task Scratch4()
        {
            var project = "public";
            var buildId = 633511;
            var server = new DevOpsServer("dnceng", Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN"));
            var build = await server.GetBuildAsync(project, buildId);
            var timeline = await server.GetTimelineAsync(project, buildId);
            var tree = TimelineTree.Create(timeline);

            var attemptMap = tree.JobNodes.GroupBy(x => x.TimelineRecord.Attempt);
            var failedJobs = tree.JobNodes.Where(x => !x.TimelineRecord.IsAnySuccess());
            var failed = tree.Nodes.Where(x => !x.TimelineRecord.IsAnySuccess());

            var previous = tree.Nodes
                .Select(x => (x.TimelineRecord, x.TimelineRecord.PreviousAttempts))
                .Where(x => x.PreviousAttempts?.Length > 0);

            var originalTimeline = await server.GetTimelineAttemptAsync(project, buildId, attempt: 1);
            var originalTree = TimelineTree.Create(originalTimeline);
            var originalFailedJobs = originalTree.JobNodes.Where(x => !x.TimelineRecord.IsAnySuccess());

        }

        private static async Task GitHubPullRequest()
        {
            var client = new GitHubClient(new ProductHeaderValue("jaredpar"));
            client.Credentials = new Credentials("jaredpar", Environment.GetEnvironmentVariable("RUNFO_GITHUB_TOKEN"));

            var pr = await client.PullRequest.Get("dotnet", "runtime", 35914);
            var runs = await client.Check.Run.GetAllForReference("dotnet", "runtime", pr.Head.Sha);
            var suites = await client.Check.Suite.GetAllForReference("dotnet", "runtime", pr.Head.Sha);
        }

        private static async Task Retry()
        {

            var organization = "dnceng";
            var project = "public";
            var buildId = 633511;
            var server = new DevOpsServer("dnceng", Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN"));
            var build = await server.GetBuildAsync(project, buildId);
            var message = server.AzureClient.CreateHttpRequestMessage(
                HttpMethod.Patch,
                $"https://dev.azure.com/{organization}/{project}/_apis/build/builds/{buildId}?retry=true&api-version=5.1");
            var repsonse = await server.AzureClient.HttpClient.SendAsync(message);

        }


        private static async Task DumpTimelineToHelix(string project, int buildId)
        {
            var server = new DevOpsServer("dnceng", Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN"));
            var queryUtil = new DotNetQueryUtil(server);
            var list = await queryUtil.ListHelixJobsAsync(project, buildId);
            var timeline = await server.GetTimelineAsync(project, buildId);
            var timelineTree = TimelineTree.Create(timeline);

            foreach (var result in list)
            {
                Console.WriteLine($"{result.Value} - {result.JobName}");
            }
        }

        private static async Task DumpTestTimesCsv()
        {
            var server = new DevOpsServer("dnceng", Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN"));
            var all = await server.ListTestRunsAsync("public", 585853);
            var debug = all.Where(x => x.Name == "Windows Desktop Debug Test32").First();
            var spanish = all.Where(x => x.Name == "Windows Desktop Spanish").First();

            await Write(debug, @"p:\temp\data-debug-class.csv");
            await Write(spanish, @"p:\temp\data-spanish-class.csv");

            async Task Write(TestRun testRun, string filePath)
            {
                var testCases = await server.ListTestResultsAsync("public", testRun.Id);

                var sorted = testCases
                    .Select(x => (ClassName: GetClassName(x), Duration:x.DurationInMs))
                    .GroupBy(x => x.ClassName)
                    .OrderBy(x => x.Key);

                var builder = new StringBuilder();
                foreach (var group in sorted)
                {
                    var time = TimeSpan.FromMilliseconds(group.Sum(x => x.Duration));
                    builder.AppendLine($"{group.Key},{time}");
                }
                File.WriteAllText(filePath, builder.ToString());
            }

            static string GetClassName(TestCaseResult testCaseResult)
            {
                var index = testCaseResult.AutomatedTestName.LastIndexOf('.');
                return testCaseResult.AutomatedTestName.Substring(0, index - 1);
            }
        }

        private static async Task Scratch2()
        {
            var server = new DevOpsServer("dnceng", await GetToken("dnceng"));
            var builds = await server.ListBuildsAsync("public", definitions: new[] { 686 }, top: 3000);
            var buildTimes = new List<(int BuildNumber, DateTime StartTime, DateTime EndTime)>();
            GetBuildTimes();

            var firstDay = buildTimes.Min(x => x.StartTime).Date;
            var lastDay = DateTime.UtcNow.Date;
            var maxCapacity = GetCapacity();
            Console.WriteLine($"Max capacity is {maxCapacity}");

            void GetBuildTimes()
            {
                using var writer = new StreamWriter(@"p:\temp\builds.csv", append: false);
                writer.WriteLine("Build Number,Start Time, End Time");
                foreach (var build in builds)
                {
                    if (build.FinishTime is null)
                    {
                        continue;
                    }

                    if (!DateTime.TryParse(build.StartTime, out var startTime) || 
                        !DateTime.TryParse(build.FinishTime, out var endTime))
                    {
                        continue;
                    }

                    writer.WriteLine($"{build.Id},{build.StartTime},{build.FinishTime}");
                    buildTimes.Add((build.Id, startTime, endTime));
                }
            }

            int GetCapacity()
            {
                using var writer = new StreamWriter(@"p:\temp\capacity.csv", append: false);
                writer.WriteLine("Time,Build Count");
                var current = firstDay;
                var max = 0;
                while (current.Date <= lastDay)
                {
                    var count = buildTimes.Count(x => current >= x.StartTime && current <= x.EndTime);
                    if (count > max)
                    {
                        max = count;
                    }

                    writer.WriteLine($"{current},{count}");
                    current = current.AddMinutes(15);
                }

                return max;
            }
        }

        private static async Task ListStaleChecks()
        {
            var gitHub = new GitHubClient(new ProductHeaderValue("MyAmazingApp"));
            gitHub.Credentials = new Credentials(await GetToken("github"));
            var apiConnection = new ApiConnection(gitHub.Connection);
            var checksClient = new ChecksClient(apiConnection);
            var server = new DevOpsServer("dnceng");
            var list = new List<string>();
            foreach (var build in await server.ListBuildsAsync("public", new[] { 196 }, top: 500))
            {
                if (build.Status == BuildStatus.Completed &&
                    build.Reason == BuildReason.PullRequest &&
                    build.FinishTime is object &&
                    DateTimeOffset.UtcNow - DateTimeOffset.Parse(build.FinishTime) > TimeSpan.FromMinutes(5))
                {
                    try
                    {
                        Console.WriteLine($"Checking {build.Repository.Id} {build.SourceVersion}");
                        // Build is complete for at  least five minutes. Results should be available 
                        var name = build.Repository.Id.Split("/");
                        var pullRequestId = int.Parse(build.SourceBranch.Split("/")[2]);
                        var prUri = $"https://github.com/{build.Repository.Id}/pull/{pullRequestId}";
                        var repository = await gitHub.Repository.Get(name[0], name[1]);

                        var pullRequest = await gitHub.PullRequest.Get(repository.Id, pullRequestId);
                        if (pullRequest.MergeableState.Value == MergeableState.Dirty ||
                            pullRequest.MergeableState.Value == MergeableState.Unknown)
                        {
                            // There are merge conflicts. This seems to confuse things a bit below. 
                            continue;
                        }

                        // Need to use the HEAD of the PR not Build.SourceVersion here. The Build.SourceVersion
                        // is the HEAD of the PR merged into HEAD of the target branch. The check suites only track
                        // the HEAD of PR
                        var response = await checksClient.Suite.GetAllForReference(repository.Id, pullRequest.Head.Sha);
                        var devOpsResponses = response.CheckSuites.Where(x => x.App.Name == "Azure Pipelines").ToList();
                        var allDone = devOpsResponses.All(x => x.Status.Value == CheckStatus.Completed);
                        if (!allDone)
                        {
                            // There are merge conflicts. This seems to confuse things a bit below. 
                            Console.WriteLine($"\t{DevOpsUtil.GetBuildUri(build)}");
                            Console.WriteLine($"\t{prUri}");
                            list.Add(prUri);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            foreach (var uri in list.ToHashSet())
            {
                Console.WriteLine(uri);
            }
        }

        private static async Task ListBuildsFullAsync()
        {
            var server = new DevOpsServer("dnceng");
            var builds1 = await server.ListBuildsAsync("public", new[] { 15 });
            var builds2 = await server.ListBuildsAsync("public", top: 10);
        }

        private static async Task DumpNgenData(int buildId)
        {
            var list = await GetNgenData(buildId);
            foreach (var data in list.OrderBy(x => x.AssemblyName))
            {
                Console.WriteLine($"{data.AssemblyName} - {data.MethodCount}");
            }
        }

        private static async Task<List<NgenEntryData>> GetNgenData(int buildId)
        {
            static int countLines(StreamReader reader)
            {
                var count = 0;
                while (null != reader.ReadLine())
                {
                    count++;
                }

                return count;
            }

            var server = new DevOpsServer("devdiv", await GetToken("azure-devdiv"));
            var project = "DevDiv";
            var stream = new MemoryStream();
            var regex = new Regex(@"(.*)-([\w.]+).ngen.txt", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            await server.DownloadArtifactAsync(project, buildId, "Build Diagnostic Files", stream);
            stream.Position = 0;
            using (var zipArchive = new ZipArchive(stream))
            {
                var list = new List<NgenEntryData>();
                foreach (var entry in zipArchive.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.Name) && entry.FullName.StartsWith("Build Diagnostic Files/ngen/"))
                    {
                        var match = regex.Match(entry.Name);
                        var assemblyName = match.Groups[1].Value;
                        var targetFramework = match.Groups[2].Value;
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream);
                        var methodCount = countLines(reader);

                        list.Add(new NgenEntryData(new NgenEntry(assemblyName, targetFramework), methodCount));
                    }
                }

                return list;
            }
        }

        private static async Task DumpNgenLogFun()
        {
            var server = new DevOpsServer("devdiv", await GetToken("azure-devdiv"));
            string project = "devdiv";
            var buildId = 2916584;
            var all = await server.ListArtifactsAsync(project, buildId);
            var buildArtifact = await server.GetArtifactAsync(project, buildId, "Build Diagnostic Files");
            var filePath = @"p:\temp\data.zip";
            var stream = await server.DownloadArtifactAsync(project, buildId, "Build Diagnostic Files");
            using var fileStream = File.Open(filePath, System.IO.FileMode.Create);
            await stream.CopyToAsync(fileStream);
        }

        private static async Task DumpTimelines(string organization, string project, int buildDefinitionId, int top)
        {
            var server = new DevOpsServer(organization);
            foreach (var build in await server.ListBuildsAsync(project, new[] { buildDefinitionId }, top: top))
            {
                Console.WriteLine($"{build.Id} {build.SourceBranch}");
                try
                {
                    await DumpTimeline(project, build.Id);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private static async Task DumpTimeline(string project, int buildId, string personalAccessToken = null)
        {
            var server = new DevOpsServer(Organization, personalAccessToken);

            var timeline = await server.GetTimelineAsync(project, buildId);
            await DumpTimeline("", timeline);
            async Task DumpTimeline(string indent, Timeline timeline)
            {
                foreach (var record in timeline.Records)
                {
                    Console.WriteLine($"{indent}Record {record.Name}");
                    if (record.Issues != null)
                    {
                        foreach (var issue in record.Issues)
                        {
                            Console.WriteLine($"{indent}{issue.Type} {issue.Category} {issue.Message}");
                        }
                    }

                    if (record.Details is object)
                    {
                        var nextIndent = indent + "\t";
                        var subTimeline = await server.GetTimelineAsync(project, buildId, record.Details.Id, record.Details.ChangeId);
                        await DumpTimeline(nextIndent, subTimeline);
                    }
                }
            }
        }

        private static async Task DumpCheckoutTimes(string organization, string project, int buildDefinitionId, int top)
        {
            var server = new DevOpsServer(organization);
            var total = 0;
            foreach (var build in await server.ListBuildsAsync(project, new[] { buildDefinitionId }, top: top))
            {
                var printed = false;
                void printBuildUri()
                {
                    if (!printed)
                    {
                        total++;
                        printed = true;
                        Console.WriteLine(DevOpsUtil.GetBuildUri(build));
                    }
                }

                try
                {
                    var timeline = await server.GetTimelineAsync(project, build.Id);
                    if (timeline is null)
                    {
                        continue;
                    }

                    foreach (var record in timeline.Records.Where(x => x.Name == "Checkout" && x.FinishTime is object && x.StartTime is object))
                    {
                        var duration = DateTime.Parse(record.FinishTime) - DateTime.Parse(record.StartTime);
                        if (duration > TimeSpan.FromMinutes(10))
                        {
                            var parent = timeline.Records.Single(x => x.Id == record.ParentId);
                            printBuildUri();
                            Console.WriteLine($"\t{parent.Name} {duration}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            Console.WriteLine($"Total is {total}");
        }

        private static async Task DumpBuild(string project, int buildId)
        {
            var server = new DevOpsServer(Organization);
            var output = @"e:\temp\logs";
            Directory.CreateDirectory(output);
            foreach (var log in await server.GetBuildLogsAsync(project, buildId))
            {
                var logFilePath = Path.Combine(output, $"{log.Id}.txt");
                Console.WriteLine($"Log Id {log.Id} {log.Type} - {logFilePath}");
                var content = await server.GetBuildLogAsync(project, buildId, log.Id);
                File.WriteAllText(logFilePath, content);

            }
        }

        private static async Task Fun()
        { 
            var server = new DevOpsServer(Organization);
            var project = "public";
            var builds = await server.ListBuildsAsync(project, definitions: new[] { 15 }, top: 10);
            foreach (var build in builds)
            {
                Console.WriteLine($"{build.Id} {build.Uri}");
                var artifacts = await server.ListArtifactsAsync(project, build.Id);
                foreach (var artifact in artifacts)
                {
                    Console.WriteLine($"\t{artifact.Id} {artifact.Name} {artifact.Resource.Type}");
                }
            }
        }

        internal readonly struct NgenEntry
        {
            internal string AssemblyName { get; }
            internal string TargetFramework { get; }

            internal NgenEntry(string assemblyName, string targetFramework)
            {
                AssemblyName = assemblyName;
                TargetFramework = targetFramework;
            }
        }

        internal readonly struct NgenEntryData
        {
            internal NgenEntry NgenEntry { get; }
            internal int MethodCount { get; }

            internal string AssemblyName => NgenEntry.AssemblyName;
            internal string TargetFramework => NgenEntry.TargetFramework;

            internal NgenEntryData(NgenEntry ngenEntry, int methodCount)
            {
                NgenEntry = ngenEntry;
                MethodCount = methodCount;
            }
        }
    }
}

