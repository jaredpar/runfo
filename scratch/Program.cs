using Azure.Identity;
using Azure.Storage.Queues;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Function;
using DevOps.Util.DotNet.Triage;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureKeyVault;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Octokit;
using Org.BouncyCastle.Asn1;
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
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

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
                if (connectionString.Contains("Catalog=runfo-test"))
                {
                    Console.WriteLine("Using SQL test");
                } 
                else if (connectionString.Contains("Catalog=runfo-prod2"))
                {
                    Console.WriteLine("Using SQL production");
                }
                else
                {
                    throw new Exception($"Connection string not recognized");
                }

                builder.UseSqlServer(connectionString, opts => opts.CommandTimeout((int)TimeSpan.FromMinutes(145).TotalSeconds).EnableRetryOnFailure());
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
        public DevOpsServer DevOpsServer { get; set; }
        public DbContextOptions<TriageContext> TriageContextOptions { get; set; }
        public TriageContext TriageContext { get; set; }
        public TriageContextUtil TriageContextUtil { get; set; }
        public IGitHubClientFactory GitHubClientFactory { get; set; }
        public DotNetQueryUtil DotNetQueryUtil { get; set; }
        public HelixServer HelixServer { get; set; }
        public FunctionQueueUtil FunctionQueueUtil { get; set; }

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public ScratchUtil()
        {
            Reset(useProduction: true);
        }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        public void Reset(string? organization = null, bool useProduction = false)
        {
            organization ??= DotNetConstants.AzureOrganization;
            var configuration = CreateConfiguration(useProduction);
            var azureToken = configuration[DotNetConstants.ConfigurationAzdoToken];
            DevOpsServer = new DevOpsServer(organization, new AuthorizationToken(AuthorizationKind.PersonalAccessToken, azureToken));

            var builder = new DbContextOptionsBuilder<TriageContext>();
            var connectionString = configuration[DotNetConstants.ConfigurationSqlConnectionString];
            var message = connectionString.Contains("triage-scratch-dev")
                ? "Using sql developer"
                : "Using sql production";
            //`builder.UseSqlServer(connectionString);
            builder.UseSqlServer(connectionString, opts => opts.CommandTimeout((int)TimeSpan.FromMinutes(15).TotalSeconds));
            //builder.UseSqlServer(connectionString, opts => opts.CommandTimeout((int)TimeSpan.FromMinutes(145).TotalSeconds));

            // builder.UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()));
            TriageContextOptions = builder.Options;
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

            DotNetQueryUtil = new DotNetQueryUtil(
                DevOpsServer,
                new AzureUtil(DevOpsServer));
            FunctionQueueUtil = new FunctionQueueUtil(configuration[DotNetConstants.ConfigurationAzureBlobConnectionString]);
            HelixServer = new HelixServer();
        }

        internal static IConfiguration CreateConfiguration(bool useProduction = false)
        {
            var keyVault = useProduction ? DotNetConstants.KeyVaultEndPointProduction : DotNetConstants.KeyVaultEndPointTest;
            var config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .AddAzureKeyVault(
                    keyVault,
                    new DefaultKeyVaultSecretManager())
                .Build();
            return config;
        }

        internal static ILogger CreateLogger() => LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Scratch");

        internal int GetDefinitionId(string definitionName) => definitionName switch
        {
            "runtime" => 686,
            "roslny" => 15,
            "coreclr" => 655,
            "sdk" => 136,
            _ => throw new Exception($"Unrecognized name: {definitionName}"),
        };

        internal async Task Scratch()
        {
            Reset(useProduction: true);
            var util = new ModelDataUtil(DotNetQueryUtil, TriageContextUtil, CreateLogger());
            var build = await DevOpsServer.GetBuildAsync("public", 3861);
            await util.EnsureModelInfoAsync(build, includeTests: true, includeAllAttempts: false);
        }

        internal async Task TestSmartUpdateAsync()
        {
            Reset(useProduction: true);
            var tracking = new TrackingGitHubUtil(GitHubClientFactory, TriageContext, SiteLinkUtil.Published, CreateLogger());
            await tracking.UpdateTrackingGitHubIssueAsync(415);
        }

        internal async Task GetHelixWaitTimes()
        {
            var project = "public";
            var count = 0;
            var map = new Dictionary<string, List<TimeSpan>>();

            await foreach (var build in DevOpsServer.EnumerateBuildsAsync(project, definitions: new[] { 15 }, statusFilter: BuildStatus.Completed))
            {
                var buildInfo = build.GetBuildInfo();
                try
                {
                    var timeline = await DevOpsServer.GetTimelineAsync(project, buildInfo.Number);
                    if (timeline is null)
                    {
                        continue;
                    }
                    var yaml = await DevOpsServer.GetYamlAsync(project, buildInfo.Number);
                    var tree = TimelineTree.Create(timeline!);
                    var helixNodes = tree
                        .JobNodes
                        .Where(x => x.Name.StartsWith("Test_"))
                        .ToList();

                    foreach (var jobNode in helixNodes)
                    {
                        if (jobNode.TimelineRecord?.Log?.Id is not { } logId)
                        {
                            continue;
                        }

                        if (await GetHelixWaitTimeAsync(logId) is not { } time)
                        {
                            continue;
                        }

                        if (!map.TryGetValue(jobNode.Name, out var list))
                        {
                            list = new();
                            map[jobNode.Name] = list;
                        }

                        list.Add(time);
                    }

                    if (++count == 100)
                    {
                        break;
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing build: {buildInfo.BuildUri}");
                    Console.WriteLine(ex.Message);
                }

                async Task<TimeSpan?> GetHelixWaitTimeAsync(int logId)
                {
                    try
                    {
                        var content = await DevOpsServer.GetBuildLogAsync(project, buildInfo.Number, logId);
                        using var reader = new StringReader(content);
                        DateTime? start = null; ;
                        while (reader.ReadLine() is { } line)
                        {
                            var parts = line.Split(new[] { ' ' }, count: 2);
                            var message = parts[1].Trim();
                            if (start is null && message.StartsWith("Starting Azure Pipelines Test"))
                            {
                                start = DateTime.Parse(parts[0]);
                            }
                            else if (message.StartsWith("Stopping Azure Pipelines Test"))
                            {
                                var end = DateTime.Parse(parts[0]);
                                return end - start;
                            }
                        }

                        return null;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            Console.WriteLine("|Job Name|Average Wasted Time (minutes)| Total Wasted Time (minutes)|");
            Console.WriteLine("|---|---|---|");
            foreach (var pair in map)
            {
                var average = pair.Value.Average(x => x.TotalSeconds) / 60;
                var total = pair.Value.Sum(x => x.TotalSeconds) / 60;
                Console.WriteLine($"|{pair.Key}|{average:N2}|{total:N2}|");
            }

            var all = map
                .Values
                .SelectMany(x => x)
                .Sum(x => x.TotalSeconds) / 60;
            Console.WriteLine($"Total Wasted minutes {all:N2}");
        }

        internal async Task FindMatchingSdkMissingBuilds()
        {
            var count = 0;
            var list = new List<string>();
            await foreach (var build in DevOpsServer.EnumerateBuildsAsync("internal", definitions: new[] { 679 }, statusFilter: BuildStatus.Completed))
            {
                try
                {
                    var buildInfo = build.GetBuildInfo();
                    Console.WriteLine($"Search {buildInfo.BuildUri}");
                    var timeline = await DevOpsServer.GetTimelineAsync(build);
                    if (timeline is null)
                    {
                        continue;
                    }

                    foreach (var issue in timeline.Records.Select(x => x.Issues).Where(x => x is object).SelectMany(x => x))
                    {
                        if (issue.Message.Contains("sdk version matching") &&
                            issue.Message.Contains("could not be found"))
                        {
                            list.Add(buildInfo.BuildUri);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                count++;
                if (count == 30)
                {
                    break;
                }
            }

            Console.WriteLine($"Hit {list.Count} builds");
            foreach (var uri in list)
            {
                Console.WriteLine(uri);
            }
        }

        internal async Task TestUpdateGitHubIssue()
        {
            Reset(useProduction: true);
            var util = new TrackingGitHubUtil(GitHubClientFactory, TriageContext, SiteLinkUtil.Published, CreateLogger());
            await util.UpdateTrackingGitHubIssuesAsync();
        }
        internal record MacJobInfo(BuildInfo BuildInfo, TimelineRecord TimelineRecord, TimeSpan Duration)
        {
            public string JobName => TimelineRecord.Name;
            public bool Succeeded = TimelineRecord.Result == TaskResult.Succeeded;
        }

        internal record MacDefinitionInfo(
            string ProjectName,
            int DefinitionId,
            string BranchName,
            string FriendlyName,
            int BuildCount,
            List<MacJobInfo> SlowMacJobInfos);

        internal async Task FindLogMacBuildAsync()
        {
            var list = new List<MacDefinitionInfo>();

            PrintAndAdd(await GetMacDefinitionInfoAsync("public", 686, "refs/heads/main", "runtime"));
            PrintAndAdd(await GetMacDefinitionInfoAsync("public", 924, "refs/heads/main", "runtime-staging"));
            PrintAndAdd(await GetMacDefinitionInfoAsync("public", 364, "refs/heads/main", "linker-ci"));
            PrintAndAdd(await GetMacDefinitionInfoAsync("internal", 679, "refs/heads/main", "dotnet-runtime-official"));
            PrintAndAdd(await GetMacDefinitionInfoAsync("internal", 679, "refs/heads/release/5.0", "dotnet-runtime-official"));

            void PrintAndAdd(MacDefinitionInfo info)
            {
                list.Add(info);

                Console.WriteLine($"Definition: {info.FriendlyName}");
                Console.WriteLine($"Branch Name: {info.BranchName}");
                Console.WriteLine($"\tTotal Builds {info.BuildCount}");

                var slowBuildCount = info
                    .SlowMacJobInfos
                    .Select(x => x.BuildInfo.BuildUri)
                    .Distinct()
                    .Count();
                var slowBuildPercent = slowBuildCount / (double)info.BuildCount;
                Console.WriteLine($"\tBuilds with at least one slow Mac build or test job {slowBuildCount} ({slowBuildPercent:p2})");

                var slowBuildNoTestCount = info
                    .SlowMacJobInfos
                    .Where(x => !x.JobName.Contains("test", StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.BuildInfo.BuildUri)
                    .Distinct()
                    .Count();
                var slowBuildNoTestPercent = slowBuildNoTestCount / (double)info.BuildCount;
                Console.WriteLine($"\tBuilds with at least one slow Mac build only job {slowBuildNoTestCount} ({slowBuildNoTestPercent:p2})");

                Console.WriteLine("Job Info");
                foreach (var job in info.SlowMacJobInfos.GroupBy(x => x.JobName).OrderByDescending(x => x.Count()))
                {
                    var count = job.Count();
                    var percent = count / (double)info.BuildCount;

                    Console.WriteLine($"\t'{job.Key}' slow on {count} builds ({percent:p2})");
                }

                Console.WriteLine("Detailed Job Info");
                foreach (var job in info.SlowMacJobInfos.GroupBy(x => x.JobName).OrderByDescending(x => x.Count()))
                {
                    Console.WriteLine($"\t{job.Key}");
                    foreach (var item in job)
                    {
                        var logUrl = item.TimelineRecord?.Log?.Url ?? "<log missing>";
                        Console.WriteLine($"\t\t{item.TimelineRecord?.Log?.Url}");
                    }
                }
            }
        }

        internal async Task<MacDefinitionInfo> GetMacDefinitionInfoAsync(string project, int definition, string branchName, string friendlyName)
        {
            Console.WriteLine($"Searching {friendlyName} {branchName}");
            var maxCount = 200;
            var count = 0;
            var jobInfoList = new List<MacJobInfo>();

            await foreach (var build in DevOpsServer.EnumerateBuildsAsync(project, definitions: new[] { definition }, statusFilter: BuildStatus.Completed, branchName: branchName))
            {
                count++;
                if (count == maxCount)
                {
                    break;
                }

                var buildInfo = build.GetBuildInfo();
                try
                {
                    Console.Write(".");
                    var timeline = await DevOpsServer.GetTimelineAsync(build);
                    if (timeline is null)
                    {
                        continue;
                    }

                    foreach (var record in timeline.Records)
                    {
                        if (IsMacJob(record) && record.GetDuration() is { } duration)
                        {
                            jobInfoList.Add(new MacJobInfo(buildInfo, record, duration));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            Console.WriteLine("");

            var slowJobInfoList = new List<MacJobInfo>();
            foreach (var group in jobInfoList.GroupBy(x => x.JobName))
            {
                if (group.Count(x => x.Succeeded) == 0)
                {
                    continue;
                }

                var average = group
                    .Where(x => x.Succeeded)
                    .Select(x => x.Duration.TotalMinutes)
                    .Average();
                var limit = average * 1.5;
                slowJobInfoList.AddRange(group.Where(x => x.Duration.TotalMinutes > limit));
            }

            return new MacDefinitionInfo(
                project,
                definition,
                branchName,
                friendlyName,
                count,
                slowJobInfoList);

            static bool IsMacJob(TimelineRecord record) => record.Type == "Job" && IsMacName(record.Name);

            static bool IsMacName(string name) =>
                name.Contains("mac", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("osx", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("ios", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("tvos", StringComparison.OrdinalIgnoreCase);
        }

        internal async Task<(string? MachineName, string? AgentName)> GetAgentInfo(string project, int buildId, TimelineRecord record)
        {
            try
            {
                if (record.Log?.Id is not { } logId)
                {
                    return default;
                }

                string? agentName = null;
                string? machineName = null;
                var content = await DevOpsServer.GetBuildLogAsync(project, buildId, logId);
                if (content is null)
                {
                    return default;
                }

                var reader = new StringReader(content);
                while (reader.ReadLine() is { } line)
                {
                    var match = Regex.Match(line, $"Agent name: '(.*)'");
                    if (match.Success)
                    {
                        agentName = match.Groups[1].Value;
                    }

                    match = Regex.Match(line, $"Agent machine name: '(.*)'");
                    if (match.Success)
                    {
                        machineName = match.Groups[1].Value;
                    }

                    if (agentName is { } && machineName is { })
                    {
                        break;
                    }
                }

                return (machineName, agentName);
            }
            catch
            {
                return default;
            }
        }

        internal async Task MeasureConnectionIssues()
        {
            var request = new SearchTimelinesRequest(@"started:~30 text:""Failed to retrieve information""");
            var modelTimelineIssuess = await request.Filter(TriageContext.ModelTimelineIssues).Include(x => x.ModelBuild).ToListAsync();
            var buildCount = modelTimelineIssuess.Select(x => x.ModelBuildAttemptId).Distinct().Count();
            var buildCountRuntime = modelTimelineIssuess.Where(x => x.ModelBuild.DefinitionNumber == 686).Select(x => x.ModelBuildAttemptId).Distinct().Count();
            var buildTotalRuntime = await (new SearchBuildsRequest(@"started:~30 definition:runtime")).Filter(TriageContext.ModelBuilds).CountAsync();

            var map = new Dictionary<DateTime, List<(ModelBuild ModelBuild, bool IsConnectionReset, string? LogUri)>>();
            foreach (var modelTimelineIssue in modelTimelineIssuess)
            {
                try
                {
                    var modelBuild = modelTimelineIssue.ModelBuild;
                    var buildInfo = modelBuild.GetBuildInfo();
                    Console.WriteLine($"Searching {buildInfo.BuildUri} {modelTimelineIssue.Id}");
                    bool isConnectionReset = false;
                    string? logUrl = null;
                    var timeline = await DevOpsServer.GetTimelineAttemptAsync(buildInfo.Project, buildInfo.Number, modelTimelineIssue.Attempt);
                    if (timeline is null)
                    {
                        timeline = await DevOpsServer.GetTimelineAsync(buildInfo.Project, buildInfo.Number);
                    }

                    if (timeline is object)
                    {
                        var record = timeline.Records.FirstOrDefault(x => x.Id == modelTimelineIssue.RecordId);
                        if (record is object && record.Log is object)
                        {
                            var log = await DevOpsServer.GetBuildLogAsync(buildInfo.Project, buildInfo.Number, record.Log.Id);
                            isConnectionReset = log.Contains("Response status code does not indicate success: 503");
                            logUrl = record.Log.Url;
                        }
                    }

                    var date = modelBuild.StartTime.Date;
                    if (!map.TryGetValue(date, out var list))
                    {
                        list = new();
                        map[date] = list;
                    }

                    list.Add((modelBuild, isConnectionReset, logUrl));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            foreach (var pair in map.OrderBy(x => x.Key))
            {
                var crCount = pair.Value.Count(x => x.IsConnectionReset);
                Console.WriteLine($"{pair.Key} Connection Reset {crCount} Other Count {pair.Value.Count - crCount}");
                foreach (var tuple in pair.Value)
                {
                    var suffix = tuple.IsConnectionReset ? "(ConnectionReset)" : "(Other)";
                    Console.WriteLine($"\t{tuple.ModelBuild.GetBuildInfo().BuildUri} {suffix}");
                }
            }

            foreach (var pair in map.OrderBy(x => x.Key))
            {
                foreach (var tuple in pair.Value)
                {
                    var buildUri = tuple.ModelBuild.GetBuildInfo().BuildUri;
                    var kind = tuple.IsConnectionReset ? "503" : "Other";
                    Console.WriteLine($"{pair.Key},{buildUri},{kind},{tuple.LogUri}");
                }
            }
        }


        internal async Task MeasureTrackingIssuePerf()
        {

            // Copied the query from the tracking issue util, paste it here, grab the SQL and 
            // get the execution plan
            var key = new BuildAttemptKey("dnceng", "public", 1101727, 1);
            var modelBuild = await TriageContextUtil.GetModelBuildQuery(key.BuildKey).Include(x => x.ModelBuildDefinition).SingleAsync();

            var testsQuery = TriageContext
                .ModelTestResults
                .Where(x => x.ModelBuildId == modelBuild.Id && x.Attempt == key.Attempt);

            var request = new SearchTestsRequest()
            {
                Started = null,
                Name = "System.Security.Cryptography.Rsa.Tests.EncryptDecrypt_Span.RoundtripEmptyArray",
            };

            var data = await request.Filter(testsQuery)
                .Select(x => new
                {
                    ModelTestResultId = x.Id,
                    JobName = x.TestRunName,
                })
                .ToListAsync()
                .ConfigureAwait(false);
        }

        internal async Task CleanMatches()
        {
            foreach (var issue in await TriageContext.ModelTrackingIssues.Where(x => x.IsActive).ToListAsync())
            {
                Console.WriteLine($"Processing {issue.IssueTitle}");
                var matches = await TriageContext.ModelTrackingIssueMatches.Where(x => x.ModelTrackingIssueId == issue.Id).ToListAsync();
                Console.WriteLine($"{matches.Count:n0} matches");

                const int limit = 50_000;
                var deleteCount = 0;
                var set = new HashSet<int>();
                foreach (var match in matches)
                {
                    if (!set.Add(match.ModelBuildAttemptId))
                    {
                        TriageContext.Remove(match);
                        deleteCount++;

                        if (deleteCount == limit)
                        {
                            Console.Write($"Deleting {deleteCount:n0} matches ... ");
                            await TriageContext.SaveChangesAsync();
                            deleteCount = 0;
                        }
                    }
                }

                if (deleteCount > 0)
                {
                    Console.Write($"Deleting {deleteCount:n0} matches ... ");
                    await TriageContext.SaveChangesAsync();
                    Console.WriteLine("done");
                }
            }
        }

        internal async Task PopulateDb()
        {
            // At 10 you start getting rate limited1
            const int maxParallel = 5;
            var logger = CreateLogger();
            var startTime = DateTime.UtcNow;
            var buildCount = 0;
            var list = new List<Task>();
            await foreach (var build in DevOpsServer.EnumerateBuildsAsync("public"))
            {
                if (build.FinishTime is null)
                {
                    continue;
                }

                var uri = build.GetBuildResultInfo().BuildUri;
                if (await TriageContextUtil.FindModelBuildAsync(build.GetBuildKey()) is object)
                {
                    Console.WriteLine($"Skipping {uri}");
                    continue;
                }

                Console.WriteLine($"Getting data for {uri}");
                list.Add(ProcessBuildAsync(build, TriageContextOptions, DotNetQueryUtil, logger));
                if (list.Count >= maxParallel)
                {
                    await Task.WhenAny(list);
                    buildCount += list.RemoveAll(x => x.IsCompleted);

                    if (buildCount % 10 == 0)
                    {
                        var rate = buildCount / (DateTime.UtcNow - startTime).TotalMinutes;
                        Console.WriteLine($"Builds populated: {buildCount}");
                        Console.WriteLine($"Builds Per Minute: {rate:n}");
                        Console.WriteLine($"Most recent build queue date: {build.GetBuildResultInfo().QueueTime.ToLocalTime()}");
                    }
                }
            }

            static async Task ProcessBuildAsync(Build build, DbContextOptions<TriageContext> options, DotNetQueryUtil queryUtil, ILogger logger)
            {
                try
                {
                    var triageContextUtil = new TriageContextUtil(new TriageContext(options));
                    var modelDataUtil = new ModelDataUtil(queryUtil, triageContextUtil, logger);
                    await modelDataUtil.EnsureModelInfoAsync(build, includeTests: true, includeAllAttempts: true);
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
        }


        internal async Task Migrate()
        {
            var util = new MigrateUtil(DotNetQueryUtil, TriageContextUtil, CreateLogger());
            await util.Migrate(@"c:\users\jaredpar\temp\migrate");
        }

        internal async Task DeleteOldBuilds(bool useProduction)
        {
            var functionUtil = new FunctionUtil(CreateLogger());

            var originalData = await GetCounts();
            while (true)
            {
                try
                {
                    Reset(useProduction: useProduction);
                    await functionUtil.DeleteOldBuilds(TriageContext, deleteMax: 100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }

                var newData = await GetCounts();
                Console.WriteLine($"Builds {newData.BuildCount} (-{originalData.BuildCount - newData.BuildCount})");
                Console.WriteLine($"Test Runs {newData.TestRunCount} (-{originalData.TestRunCount - newData.TestRunCount})");
                Console.WriteLine($"Test Results {newData.TestCount} (-{originalData.TestCount - newData.TestCount})");
                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            async Task<(int BuildCount, int TestRunCount, int TestCount)> GetCounts()
            {
                var buildCount = await TriageContext.ModelBuilds.CountAsync();
                var testRunsCount = await TriageContext.ModelTestRuns.CountAsync();
                var testCount = await TriageContext.ModelTestResults.CountAsync();
                return (buildCount, testRunsCount, testCount);
            }
        }

        internal async Task RetriesWork()
        {
            var definitions = new string[] { "runtime", "aspnetcore-ci", "roslyn" };
            var date = DateTime.Now - TimeSpan.FromDays(14);
            var builds = await TriageContext
                .ModelBuilds
                .Where(x => x.StartTime > date && x.BuildKind == ModelBuildKind.MergedPullRequest)
                .Include(x => x.ModelBuildAttempts)
                .Select(x => new
                {
                    x.DefinitionName,
                    x.BuildResult,
                    AttemptCount = x.ModelBuildAttempts.Count()
                })
                .ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("Build Definition,Pass on Attempt 1,Pass on any Attempt");
            foreach (var group in builds.GroupBy(x => x.DefinitionName))
            {
                double firstAttemptCount = group.Count(x => IsAnySuccess(x.BuildResult) && x.AttemptCount == 1);
                double anyAttemptCount = group.Count(x => IsAnySuccess(x.BuildResult) && x.AttemptCount >= 1);
                double total = group.Count();
                builder.AppendLine($"{group.Key},{(firstAttemptCount / total):P1},{(anyAttemptCount / total):P1}");
            }

            static bool IsAnySuccess(ModelBuildResult? result) => result is { } r && (r == ModelBuildResult.Succeeded || r == ModelBuildResult.PartiallySucceeded);

            File.WriteAllText(@"p:\temp\data.csv", builder.ToString());
        }

        /// <summary>
        /// This is a function to populate all of the <see cref="ModelBuildDefinition.DefinitionName"/> columns that
        /// existed before the data was de-normalized.
        /// </summary>
        /// <returns></returns>
        internal async Task PopulateDefinitionColumns()
        {
            var total = await TriageContext
                    .ModelBuilds
                    .Where(x => x.DefinitionName == "")
                    .CountAsync();

            Console.WriteLine($"Total {total:N0}");
            var count = 0;
            var increment = 500;
            while (true)
            {
                Console.WriteLine($"Completed {count:N0} Remaining {(total - count):N0}");
                var builds = await TriageContext
                    .ModelBuilds
                    .Where(x => x.DefinitionName == "")
                    .Include(x => x.ModelBuildDefinition)
                    .Take(increment)
                    .ToListAsync();

                if (builds.Count == 0)
                {
                    break;
                }

                foreach (var build in builds)
                {
                    build.DefinitionName = build.ModelBuildDefinition.DefinitionName;
                    build.DefinitionNumber = build.ModelBuildDefinition.DefinitionNumber;
                }

                await TriageContext.SaveChangesAsync();
                count += increment;
            }
        }

        internal async Task DumpDarcPublishData()
        {
            int count = 0;
            var builder = new StringBuilder();
            await foreach( var build in DevOpsServer.EnumerateBuildsAsync("internal", new[] { 679 }, queryOrder: BuildQueryOrder.QueueTimeDescending))
            {
                if (build.GetTargetBranch() != "master")
                {
                    continue;
                }

                var timeline = await DevOpsServer.GetTimelineAsync(build);
                if (timeline is null)
                {
                    continue;
                }

                var record = timeline.Records.FirstOrDefault(x => x.Name == "Publish Using Darc");
                if (record is null || 
                    record.StartTime is null || 
                    record.FinishTime is null || 
                    record.Result != TaskResult.Succeeded)
                {
                    continue;
                }

                var time = DevOpsUtil.ConvertFromRestTime(record.FinishTime) - DevOpsUtil.ConvertFromRestTime(record.StartTime);
                builder.AppendLine($"{build.GetBuildKey().BuildUri} - {time:hh\\:mm\\:ss}");
                if (++count > 100)
                {
                    break;

                }
            }

            File.WriteAllText(@"p:\temp\data.txt", builder.ToString());
        }

        internal async Task PopulateModelTrackingIssue(string buildQuery, int modelTrackingIssueId)
        {
            var request = new SearchBuildsRequest();
            request.ParseQueryString(buildQuery);

            var query = request.Filter(TriageContext.ModelBuilds)
                .Include(x => x.ModelBuildAttempts);
            var logger = CreateLogger();

            foreach (var modelBuild in await query.ToListAsync())
            {
                foreach (var attempt in modelBuild.ModelBuildAttempts)
                {
                    Console.WriteLine($"Triage {attempt.GetBuildAttemptKey().BuildUri}");
                    var trackingIssueUtil = new TrackingIssueUtil(HelixServer, DotNetQueryUtil, TriageContextUtil, logger);
                    await trackingIssueUtil.TriageAsync(attempt.GetBuildAttemptKey(), modelTrackingIssueId);
                }
            }

            await FunctionQueueUtil.QueueUpdateIssueAsync(
                await TriageContext.ModelTrackingIssues.SingleAsync(x => x.Id == modelTrackingIssueId),
                delay: null);
        }

        internal async Task TestTrackingIssueUtil(int buildNumber, int modelTrackingIssueId)
        {
            var trackingIssueUtil = new TrackingIssueUtil(HelixServer, DotNetQueryUtil, TriageContextUtil, CreateLogger());
            var buildKey = new BuildKey(DotNetConstants.AzureOrganization, DotNetConstants.DefaultAzureProject, buildNumber);
            await trackingIssueUtil.TriageAsync(buildKey, modelTrackingIssueId);
        }


        /// <summary>
        /// Now that builds have associated issues need to migrate all of the existing tracking issues 
        /// to associated issues
        /// </summary>
        /// <returns></returns>
        internal async Task MigrateTrackingToAssociatedIssues()
        {
            var list = await TriageContext
                .ModelTrackingIssueResults
                .Where(x => x.IsPresent && x.ModelTrackingIssue.GitHubIssueNumber.HasValue)
                .Include(x => x.ModelTrackingIssue)
                .Include(x => x.ModelBuildAttempt)
                .ThenInclude(x => x.ModelBuild)
                .ToListAsync();
            var set = new HashSet<(BuildKey, GitHubIssueKey)>();

            foreach (var result in list)
            {
                var buildKey = result.ModelBuildAttempt.ModelBuild.GetBuildKey();
                var issueKey = result.ModelTrackingIssue.GetGitHubIssueKey()!.Value;
                if (issueKey.IssueUri.Contains("jaredpar"))
                {
                    continue;
                }

                if (set.Add((buildKey, issueKey)))
                {
                    Console.WriteLine($"{buildKey.BuildUri} - {issueKey.IssueUri}");
                    var modelBuild = await TriageContextUtil.GetModelBuildAsync(buildKey);
                    await TriageContextUtil.EnsureGitHubIssueAsync(modelBuild, issueKey, saveChanges: true);
                }
            }
        }

        internal async Task PopulateTestResultsWithNewData(int definitionId, int limit)
        {
            var builds = await TriageContext
                .ModelBuilds
                .Where(x => x.ModelBuildDefinition.DefinitionNumber == definitionId && x.BuildResult == ModelBuildResult.Failed)
                .OrderByDescending(x => x.BuildNumber)
                .Take(limit)
                .ToListAsync();
            foreach (var modelBuild in builds)
            {
                var buildInfo = modelBuild.GetBuildInfo();

                var modelTestResults = await TriageContext
                    .ModelTestResults
                    .Where(x => x.ModelBuildId == modelBuild.Id)
                    .Include(x => x.ModelTestRun)
                    .ToListAsync();
                if (modelTestResults.Where(x => !x.IsSubResult).Any(x => !string.IsNullOrEmpty(x.ErrorMessage)))
                {
                    continue;
                }

                Console.WriteLine($"Populating {buildInfo.BuildUri}");

                try
                {
                    foreach (var group in modelTestResults.GroupBy(x => x.ModelTestRunId))
                    {
                        var modelTestRun = group.First().ModelTestRun;
                        foreach (var testCaseResult in await DevOpsServer.ListTestResultsAsync(buildInfo.Project, modelTestRun.TestRunId, DevOpsUtil.FailedTestOutcomes, includeSubResults: true))
                        {
                            var modelTestResult = modelTestResults.FirstOrDefault(x => x.TestFullName == testCaseResult.TestCaseTitle);
                            if (modelTestResult is object)
                            {
                                modelTestResult.ErrorMessage = testCaseResult.ErrorMessage ?? "";
                            }

                            if (testCaseResult.SubResults is { } subResults)
                            {
                                if (modelTestResult is object)
                                {
                                    modelTestResult.IsSubResultContainer = true;
                                }

                                foreach (var subResult in subResults)
                                {
                                    var iterationTestResult = new ModelTestResult()
                                    {
                                        TestFullName = testCaseResult.TestCaseTitle,
                                        Outcome = subResult.Outcome,
                                        ModelTestRun = modelTestRun,
                                        ModelBuild = modelBuild,
                                        ErrorMessage = subResult.ErrorMessage ?? "",
                                        IsSubResultContainer = false,
                                        IsSubResult = true
                                    };

                                    TriageContext.ModelTestResults.Add(iterationTestResult);
                                }

                            }
                            await TriageContext.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        internal async Task DumpingTestData()
        {
            var logger = CreateLogger();
            var trackingUtil = new TrackingIssueUtil(HelixServer, DotNetQueryUtil, TriageContextUtil, logger);

            // await DumpOutcomes(27335328);
            var thatBuild = await DevOpsServer.GetBuildAsync("public", 855241);
            await DumpTestData(thatBuild);

            /*
            var builds = await DotNetQueryUtil.ListBuildsAsync(count: 100, definitions: new[] { 15 });
            foreach (var build in builds)
            {

                if (build.Result != BuildResult.Failed)
                {
                    continue;
                }

                await DumpTestData(build);
            }

            async Task DumpOutcomes(int testRunId)
            {
                var set = new HashSet<string>();
                await foreach (var testCaseResult in DevOpsServer.EnumerateTestResultsAsync("public", runId: testRunId, detail: ResultDetail.SubResults))
                {
                    set.Add(testCaseResult.Outcome);
                    if (testCaseResult.TestCaseTitle.Contains("TestNonTrailingNamedArgumentInCSharp7_2"))
                    {

                    }
                }

                foreach (var outcome in set)
                {
                    Console.WriteLine(outcome);
                }
            }
            */

            async Task DumpTestData(Build build)
            { 
                foreach (var testRun in await DevOpsServer.ListTestRunsAsync("public", build.Id, detail: ResultDetail.SubResults))
                {
                    var testCaseResults = await DevOpsServer.ListTestResultsAsync("public", testRun.Id, DevOpsUtil.FailedTestOutcomes, includeSubResults: true);
                    foreach (var testCaseResult in testCaseResults)
                    {
                        Console.WriteLine($"{testCaseResult.TestCaseTitle} - {testCaseResult.ErrorMessage}");
                        if (testCaseResult.SubResults is object)
                        {
                            foreach (var subResult in testCaseResult.SubResults)
                            {
                                Console.WriteLine($"   {subResult.DisplayName} - {subResult.ErrorMessage}");
                            }
                        }
                    }
                }
            }
        }

        internal async Task PopulateModelBuildDefinitionTable()
        {
            var definitions = await DevOpsServer.ListDefinitionsAsync("public");
            foreach (var definition in definitions)
            {
                Console.WriteLine(definition.Name);
                await TriageContextUtil.EnsureBuildDefinitionAsync(definition.GetDefinitionInfo(DevOpsServer.Organization));
            }
        }

        internal async Task PopulateDb(int count, int definitionId, bool includeTests, bool includeTriage)
        {
            var logger = CreateLogger();
            var trackingUtil = new TrackingIssueUtil(HelixServer, DotNetQueryUtil, TriageContextUtil, logger);
            var builds = await DotNetQueryUtil.ListBuildsAsync(count: count, definitions: new[] { definitionId });
            foreach (var build in builds)
            {
                try
                {
                    var uri = build.GetBuildResultInfo().BuildUri;
                    Console.WriteLine($"Getting data for {uri}");
                    var modelDataUtil = new ModelDataUtil(DotNetQueryUtil, TriageContextUtil, logger);
                    var buildAttemptKey = await modelDataUtil.EnsureModelInfoAsync(build, includeTests: includeTests);

                    if (includeTriage)
                    {
                        Console.WriteLine($"Triaging {uri}");
                        await trackingUtil.TriageAsync(buildAttemptKey);
                    }
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

        internal async Task PopulateTimelines()
        {
            var triageContextUtil = new TriageContextUtil(TriageContext);
            foreach (var build in await DotNetQueryUtil.ListBuildsAsync(definitionId: 686, includePullRequests: true))
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

            foreach (var build in await queryUtil.ListBuildsAsync(definitionId: GetDefinitionId("roslyn"), count: 100, branch: "master", before: "2020/4/22"))
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

            foreach (var build in await queryUtil.ListBuildsAsync(definitionId: 686, count: 30, includePullRequests: true))
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

            foreach (var build in await queryUtil.ListBuildsAsync(definitionId: 686, count: 100))
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
            foreach (var build in await queryUtil.ListBuildsAsync(definitionId: 686, count: 100, includePullRequests: true))
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
            var testRuns = await queryUtil.ListDotNetTestRunsAsync(build, includeSubResults: true);
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
            var server = new DevOpsServer(DotNetConstants.AzureOrganization);
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
            var server = new DevOpsServer(DotNetConstants.AzureOrganization);
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

