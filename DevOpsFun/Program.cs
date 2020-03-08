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
using DevOpsFun;
using DevOps.Util.DotNet;
using System.Net;
using System.ComponentModel.DataAnnotations;
using Octokit;
using System.Dynamic;
using System.Net.Http;
using System.Text;

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
            var server = new DevOpsServer("dnceng", await GetToken("dnceng"));
            await foreach (var build in server.EnumerateBuildsAsync("public", queryOrder: BuildQueryOrder.FinishTimeDescending, statusFilter: BuildStatus.Completed))
            {
                try
                {


                }
                catch (System.Exception)
                {
                    
                    throw;
                }
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

        private static async Task UploadCloneTime()
        {
            using var util = new CloneTimeUtil(await GetToken("scratch-db"));
            await util.UpdateDatabaseAsync();
        }

        private static async Task DumpTestTimes()
        {
            using var util = new RunTestsUtil(await GetToken("scratch-db"));
            foreach (var build in (await util.ListBuildsAsync(top: 20)).Where(x => x.Result == BuildResult.Succeeded))
            {
                Console.Write(DevOpsUtil.GetBuildUri(build));
                Console.Write(" ");
                try
                {
                    var buildTestTime = await util.GetBuildTestTimeAsync(build);
                    var milliseconds = buildTestTime.Jobs.Sum(x => x.Duration.TotalMilliseconds);
                    Console.Write(TimeSpan.FromMilliseconds(milliseconds));
                    Console.Write(" ");
                    var max = buildTestTime.Jobs.Max(x => x.Duration.TotalMilliseconds);
                    Console.WriteLine(TimeSpan.FromMilliseconds(max));
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            await util.UpdateDatabaseAsync(top: 100);
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

