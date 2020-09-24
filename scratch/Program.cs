using Azure.Storage.Queues;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Function;
using DevOps.Util.DotNet.Triage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// [assembly: Microsoft.Extensions.Configuration.UserSecrets.UserSecretsId("67c4a872-5dd7-422a-acad-fdbe907ace33")]

namespace Scratch
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var scratchUtil = new ScratchUtil();
            await scratchUtil.Scratch();
        }

        // This entry point exists so that `dotnet ef database` and `migrations` has an 
        // entry point to create TriageDbContext
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host
                .CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddDbContext<TriageContext>(options => Config(options));
                });

            static void Config(DbContextOptionsBuilder builder)
            {
                var configuration = ScratchUtil.CreateConfiguration();
                var connectionString = configuration[DotNetConstants.ConfigurationSqlConnectionString];
                var message = connectionString.Contains("triage-scratch-dev")
                    ? "Using sql dev"
                    : "Using sql production";
                Console.WriteLine(message);
                builder.UseSqlServer(connectionString);
            }
        }
    }

    internal sealed class FakeGitHubClientFactory : IGitHubClientFactory
    {
        public GitHubClient GitHubClient { get; }

        public FakeGitHubClientFactory(GitHubClient gitHubClient)
        {
            GitHubClient = gitHubClient;
        }

        public Task<IGitHubClient> CreateForAppAsync(string owner, string repository) => Task.FromResult<IGitHubClient>(GitHubClient);
    }

    internal sealed class ScratchUtil
    { 
        public static string DefaultOrganization { get; set; } = "dnceng";

        public DevOpsServer DevOpsServer { get; set; }
        public TriageContext TriageContext { get; set; }
        public TriageContextUtil TriageContextUtil { get; set; }
        public IGitHubClientFactory GitHubClientFactory { get; set; }
        public DotNetQueryUtil DotNetQueryUtil { get; set; }
        public BlobStorageUtil BlobStorageUtil { get; set; }
        public FunctionQueueUtil FunctionQueueUtil { get; set; }


#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public ScratchUtil()
        {
            Reset(DefaultOrganization);
        }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        public void Reset(string organization)
        {
            var configuration = CreateConfiguration();
            var azureToken = configuration["RUNFO_AZURE_TOKEN"];
            DevOpsServer = new DevOpsServer(organization, new AuthorizationToken(AuthorizationKind.PersonalAccessToken, azureToken));

            var builder = new DbContextOptionsBuilder<TriageContext>();
            builder.UseSqlServer(configuration[DotNetConstants.ConfigurationSqlConnectionString]);
            // builder.UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()));
            TriageContext = new TriageContext(builder.Options);
            TriageContextUtil = new TriageContextUtil(TriageContext);

            var gitHubClient = new GitHubClient(new ProductHeaderValue("runfo-scratch-app"));
            var value = configuration["RUNFO_GITHUB_TOKEN"];
            if (value is object)
            {
                var both = value.Split(new[] { ':' }, count: 2);
                gitHubClient.Credentials = new Credentials(both[0], both[1]);
            }
            GitHubClientFactory = new FakeGitHubClientFactory(gitHubClient);

            BlobStorageUtil = new BlobStorageUtil(organization, configuration[DotNetConstants.ConfigurationAzureBlobConnectionString]);

            DotNetQueryUtil = new DotNetQueryUtil(
                DevOpsServer,
                new CachingAzureUtil(BlobStorageUtil, DevOpsServer));
            FunctionQueueUtil = new FunctionQueueUtil(configuration[DotNetConstants.ConfigurationAzureBlobConnectionString]);
        }

        internal static IConfiguration CreateConfiguration()
        {
            var config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .AddEnvironmentVariables()
                .Build();
                return config;
        }

        internal static ILogger CreateLogger() => LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Scratch");


        internal async Task Scratch()
        {
            var issue = await TriageContext.ModelTrackingIssues.Where(x => x.Id == 32).SingleAsync();
            var buildsRequest = new SearchBuildsRequest()
            {
                Definition = "roslyn-ci",
                Started = new DateRequestValue(21),
            };
            var testsRequest = new SearchTestsRequest()
            {
                Name = "GetDocumentTextChangesAsync",
            };

            IQueryable<ModelTestResult> query = TriageContext.ModelTestResults;
            query = buildsRequest.Filter(query);
            query = testsRequest.Filter(query);
            var results = await query.Select(x => x.ModelBuild).ToListAsync();

            foreach (var build in results)
            {
                Console.WriteLine($"Triaging {build.GetBuildKey().BuildUri}");
                var util = new TrackingIssueUtil(DotNetQueryUtil, TriageContextUtil, CreateLogger());
                await util.TriageAsync(build.GetBuildKey(), issue.Id);
            }

            await FunctionQueueUtil.QueueUpdateIssueAsync(issue, delay: null);
        }

        internal async Task PopulateDb()
        {
            var logger = CreateLogger();
            var trackingUtil = new TrackingIssueUtil(DotNetQueryUtil, TriageContextUtil, logger);
            var builds = await DotNetQueryUtil.ListBuildsAsync(count: 20, definitions: new[] { 15 });
            foreach (var build in builds)
            {
                try
                {
                    var uri = build.GetBuildResultInfo().BuildUri;
                    Console.WriteLine($"Getting data for {uri}");
                    var modelDataUtil = new ModelDataUtil(DotNetQueryUtil, TriageContextUtil, logger);
                    var buildAttemptKey = await modelDataUtil.EnsureModelInfoAsync(build, includeTests: true);
                    Console.WriteLine($"Triaging {uri}");
                    await trackingUtil.TriageAsync(buildAttemptKey);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    if (ex.InnerException is { } ie)
                    {
                        Console.WriteLine($"Inner Error: {ie.Message}");
                    }
                }
            }

            // await DumpMachineUsage();
            // await DumpJobFailures();
            // await DumpRoslynTestTimes();

            // var builds = await server.ListBuildsAsync("public", definitions: new[] { 731 }, branchName: "refs/pull/39837/merge", repositoryId: "dotnet/runtime", repositoryType: "github");
            //var factory = new GitHubClientFactory(CreateConfiguration());
            //var gitHubClient = await factory.CreateForAppAsync("jaredpar", "devops-util");
            //var comment = await gitHubClient.Issue.Comment.Create("jaredpar", "devops-util", 5, "This is a test comment");




            /*
            var blobClient = BlobStorageUtil;
            foreach (var build in await DotNetQueryUtil.ListBuildsAsync("-d runtime -c 10 -pr"))
            {
                var buildInfo = build.GetBuildInfo();
                var timelines = await DevOpsServer.ListTimelineAttemptsAsync(buildInfo.Project, buildInfo.Number);
                await blobClient.SaveTimelineAsync(buildInfo.Project, buildInfo.Number, timelines);

                // var found = await blobClient.GetTimelineAsync(DevOpsServer.Organization, buildInfo.Project, buildInfo.Number);
                // Console.WriteLine(found);
            }
    
            */
        }

        internal async Task ReprocessPoison(string queueName)
        {
            var connectionString = CreateConfiguration()["AzureWebJobsStorage"];
            var client = new QueueClient(connectionString, queueName);
            var poisonClient = new QueueClient(connectionString, $"{queueName}-poison");
            do
            {
                try
                {
                    var cts = new CancellationTokenSource();
                    var response = await poisonClient.ReceiveMessagesAsync(cts.Token);
                    if (response.Value.Length == 0)
                    {
                        break;
                    }

                    foreach (var message in response.Value)
                    {
                        Console.WriteLine($"Processing {message.MessageText}");
                        await client.SendMessageAsync(message.MessageText);
                        await poisonClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
            while (true) ;
        }

        internal async Task QueryProfile()
        {
            var searchBuildsRequest = new SearchBuildsRequest()
            {
                Definition = "15",
            };

            var results = await searchBuildsRequest
                .Filter(TriageContext.ModelTimelineIssues)
                .OrderByDescending(x => x.ModelBuild.BuildNumber)
                .Take(50)
                .ToListAsync();

            Console.WriteLine(results.Count);
        }

        internal async Task PopulateTimelines()
        {
            var triageContextUtil = new TriageContextUtil(TriageContext);
            foreach (var build in await DotNetQueryUtil.ListBuildsAsync(definition: "runtime", includePullRequests: true))
            {
                var buildInfo = build.GetBuildResultInfo();
                try
                {
                    Console.WriteLine($"Populating {buildInfo.BuildUri}");
                    var list = await DevOpsServer.ListTimelineAttemptsAsync(buildInfo.Project, buildInfo.Number);
                    foreach (var timeline in list)
                    {
                        await triageContextUtil.EnsureBuildAttemptAsync(buildInfo, timeline);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error with {buildInfo.Number}: {ex.Message}");
                    if (ex.InnerException is { } ie)
                    {
                        Console.WriteLine($"Inner Error with {buildInfo.Number}: {ie.Message}");
                    }
                }
            }
        }

        internal async Task ExhaustTimelineAsync()
        {
            var builds = await DotNetQueryUtil.ListBuildsAsync(count: 200, definitions: null);
            foreach (var build in builds)
            {
                try
                {
                    var buildInfo = build.GetBuildResultInfo();
                    Console.WriteLine(buildInfo.BuildUri);
                    var timeline = await DevOpsServer.GetTimelineAsync(build);
                    if (timeline?.GetAttempt() is int attempt && attempt > 1)
                    {
                        while (attempt > 1)
                        {
                            attempt--;
                            var oldTimeline = await DevOpsServer.GetTimelineAttemptAsync(buildInfo.Project, buildInfo.Number, attempt);
                            var tree = TimelineTree.Create(oldTimeline!);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);

                }
            }
        }

        internal void BuildMergedPullRequestBuilds()
        {
            /*
            var functionUtil = new FunctionUtil();
            var gitHubUtil = new GitHubUtil(GitHubClient);
            var triageContextUtil = new TriageContextUtil(TriageContext);
            foreach (var modelBuild in await TriageContext.ModelBuilds.Include(x => x.ModelBuildDefinition).Where(x => x.IsMergedPullRequest).ToListAsync())
            {
                try
                {
                    Console.WriteLine(modelBuild.BuildNumber);
                    var build = await DevOpsServer.GetBuildAsync(modelBuild.ModelBuildDefinition.AzureProject, modelBuild.BuildNumber);
                    await triageContextUtil.EnsureBuildAsync(build.GetBuildInfo());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            */

        }

#nullable disable

        public async Task DumpRoslynTestTimes()
        {
            var server = DevOpsServer;
            var queryUtil = DotNetQueryUtil;

            foreach (var build in await queryUtil.ListBuildsAsync(definition: "roslyn", count: 100, branch: "master", before: "2020/4/22"))
            {
                var buildInfo = build.GetBuildResultInfo();
                try
                {
                    await DumpTestTimeAsync(buildInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error with {buildInfo.Number}: {ex.Message}");
                }
            }

            ;

            async Task DumpTestTimeAsync(BuildResultInfo buildInfo)
            {
                var timeline = await server.GetTimelineAsync(buildInfo.Project, buildInfo.Number);
                if (timeline is null)
                {
                    return;
                }

                var tree = TimelineTree.Create(timeline);
                var record = timeline.Records
                    .Where(
                        x => x.Name == "Build and Test" &&
                        tree.TryGetParent(x, out var parent) &&
                        parent.Name == "Windows_Desktop_Unit_Tests debug_32")
                    .FirstOrDefault();
                if (record is null)
                {
                    Console.WriteLine($"Can't get record for {buildInfo.BuildUri}");
                }

                if (record.IsAnyFailed())
                {
                    Console.WriteLine($"tests failed {buildInfo.BuildUri}");
                    return;
                }

                var duration = TryGetDuration(record.StartTime, record.FinishTime);

                var log = await server.DownloadFileTextAsync(record.Log.Url);
                // Console.WriteLine(log);
                using var reader = new StringReader(log);
                var regex = new Regex(@"Test execution time: (.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                do
                {
                    var line = reader.ReadLine();
                    if (line is null)
                    {
                        Console.WriteLine($"Can't get time for {buildInfo.BuildUri}");
                        break;
                    }

                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        Console.WriteLine($"{buildInfo.BuildUri} {duration} {match.Groups[1].Value}");
                        break;
                    }
                } while (true);
            }

            static TimeSpan? TryGetDuration(string startTime, string finishTime)
            {
                if (startTime is null ||
                    finishTime is null ||
                    !DateTime.TryParse(startTime, out var s) ||
                    !DateTime.TryParse(finishTime, out var f))
                {
                    return null;
                }

                return f - s;
            }
        }

        public async Task DumpTimelines()
        {
            var server = DevOpsServer;
            var queryUtil = DotNetQueryUtil;

            foreach (var build in await queryUtil.ListBuildsAsync(definition: "runtime", count: 30, includePullRequests: true))
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

        public async Task DumpJobFailures()
        {
            var server = DevOpsServer;
            var queryUtil = DotNetQueryUtil;
            var jobCount = 0;
            var testFailCount = 0;
            var otherFailCount = 0;

            foreach (var build in await queryUtil.ListBuildsAsync(definition: "runtime", count: 100))
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

                    if (helixJobs.Any(x => x.Record.JobName == job.Name))
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

        private async Task DumpDownloadTimes()
        {
            var server = DevOpsServer;
            var queryUtil = DotNetQueryUtil;

            Console.WriteLine("Build Uri,Pull Request,Minutes");
            foreach (var build in await queryUtil.ListBuildsAsync(definition: "runtime", count: 100, includePullRequests: true))
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
                        var buildInfo = build.GetBuildResultInfo();
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

        public async Task DumpTestTotals()
        {
            var project = "public";
            var buildId = 642971;
            var server = DevOpsServer;
            var queryUtil = DotNetQueryUtil;
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

        private async Task Scratch4()
        {
            var project = "public";
            var buildId = 633511;
            var server = DevOpsServer;
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

        private async Task DumpTimelineToHelix(string project, int buildId)
        {
            var server = DevOpsServer;
            var queryUtil = DotNetQueryUtil;
            var list = await queryUtil.ListHelixJobsAsync(project, buildId);
            var timeline = await server.GetTimelineAsync(project, buildId);
            var timelineTree = TimelineTree.Create(timeline);

            foreach (var result in list)
            {
                Console.WriteLine($"{result.HelixJob} - {result.Record.JobName}");
            }
        }

        private async Task DumpTestTimesCsv()
        {
            var server = DevOpsServer;
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

        private async Task Scratch2()
        {
            var server = DevOpsServer;
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

        private async Task ListStaleChecks()
        {
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
                        if (build.GetBuildResultInfo().PullRequestKey is { } prKey)
                        {
                            var gitHub = await GitHubClientFactory.CreateForAppAsync(prKey.Organization, prKey.Repository);
                            var apiConnection = new ApiConnection(gitHub.Connection);
                            var checksClient = new ChecksClient(apiConnection);
                            var repository = await gitHub.Repository.Get(prKey.Organization, prKey.Repository);

                            var pullRequest = await gitHub.PullRequest.Get(repository.Id, prKey.Number);
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
                                Console.WriteLine($"\t{prKey.PullRequestUri}");
                                list.Add(prKey.PullRequestUri);
                            }
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

        private async Task DumpNgenData(int buildId)
        {
            var list = await GetNgenData(buildId);
            foreach (var data in list.OrderBy(x => x.AssemblyName))
            {
                Console.WriteLine($"{data.AssemblyName} - {data.MethodCount}");
            }
        }

        private async Task<List<NgenEntryData>> GetNgenData(int buildId)
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

            var server = DevOpsServer;
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

        private async Task DumpNgenLogFun()
        {
            var server = DevOpsServer;
            string project = "devdiv";
            var buildId = 2916584;
            var all = await server.ListArtifactsAsync(project, buildId);
            var buildArtifact = await server.GetArtifactAsync(project, buildId, "Build Diagnostic Files");
            var filePath = @"p:\temp\data.zip";
            var stream = await server.DownloadArtifactAsync(project, buildId, "Build Diagnostic Files");
            using var fileStream = File.Open(filePath, System.IO.FileMode.Create);
            await stream.CopyToAsync(fileStream);
        }

        private async Task DumpTimelines(string organization, string project, int buildDefinitionId, int top)
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

        private async Task DumpTimeline(string project, int buildId, string personalAccessToken = null)
        {
            var server = DevOpsServer;

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
            var server = new DevOpsServer(DefaultOrganization);
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
            var server = new DevOpsServer(DefaultOrganization);
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

