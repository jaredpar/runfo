using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Mono.Options;
using Octokit;
using static Runfo.RuntimeInfoUtil;

namespace Runfo
{
    public class Program
    {
        internal static async Task<int> Main(string[] args)
        {
            var token = Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN");
            var disableCache = false;
            var optionSet = new OptionSet()
        {
            { "token=", "The Azure DevOps personal access token", t => token = t },
            { "dc|disable-cache", "Disable caching", dc => disableCache = dc is object }
        };

            try
            {
                args = optionSet.Parse(args).ToArray();
                if (token is null)
                {
                    token = await GetPersonalAccessTokenFromFile(DotNetUtil.AzureOrganization);
                }

                var server = new DevOpsServer(DotNetUtil.AzureOrganization, new AuthorizationToken(AuthorizationKind.PersonalAccessToken, token!));
                var azureUtil = new CachingAzureUtil(
                    new LocalAzureStorageUtil(DotNetUtil.AzureOrganization, RuntimeInfoUtil.CacheDirectory),
                    new AzureUtil(server));

                var runtimeInfo = new RuntimeInfo(server, azureUtil);

                // Kick off a collection of the file system cache
                var collectTask = runtimeInfo.CollectCache();
                try
                {
                    if (args.Length == 0)
                    {
                        await runtimeInfo.PrintBuildResults(Array.Empty<string>());
                        return ExitSuccess;
                    }

                    return await RunCommand(runtimeInfo, args);
                }
                finally
                {
                    await collectTask;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                return ExitFailure;
            }

            async Task<int> RunCommand(RuntimeInfo runtimeInfo, string[] args)
            {
                var command = args[0].ToLower();
                var commandArgs = args.Skip(1);
                switch (command)
                {
                    case "definitions":
                        runtimeInfo.PrintBuildDefinitions();
                        return ExitSuccess;
                    case "status":
                        await runtimeInfo.PrintBuildResults(commandArgs);
                        return ExitSuccess;
                    case "artifacts":
                        return await runtimeInfo.PrintArtifacts(commandArgs);
                    case "builds":
                        return await runtimeInfo.PrintBuilds(commandArgs);
                    case "pr-builds":
                        return await runtimeInfo.PrintPullRequestBuilds(commandArgs);
                    case "tests":
                    case "search-tests":
                        return await runtimeInfo.PrintFailedTests(commandArgs);
                    case "helix":
                        return await runtimeInfo.PrintHelix(commandArgs);
                    case "helix-jobs":
                        return await runtimeInfo.PrintHelixJobs(commandArgs);
                    case "timeline":
                        return await runtimeInfo.PrintTimeline(commandArgs);
                    case "search-timeline":
                        return await runtimeInfo.PrintSearchTimeline(commandArgs);
                    case "search-helix":
                        return await runtimeInfo.PrintSearchHelix(commandArgs);
                    case "search-buildlog":
                        return await runtimeInfo.PrintSearchBuildLogs(commandArgs);
                    case "yml":
                    case "yaml":
                        return await runtimeInfo.PrintBuildYaml(commandArgs);
                    case "machines":
                        return await runtimeInfo.PrintMachines(commandArgs);
                    case "clear-cache":
                        return runtimeInfo.ClearCache();
                    default:
                        Console.WriteLine($"Error: {command} is not recognized as a valid command");
                        ShowHelp();
                        return ExitFailure;
                }

            }

            void ShowHelp()
            {
                Console.WriteLine("runfo");
                Console.WriteLine("=== Commands ===");
                Console.WriteLine("  status            Print build definition status");
                Console.WriteLine("  definitions       Print build definition info");
                Console.WriteLine("  artifacts         Print artifact info for a given build");
                Console.WriteLine("  builds            Print builds");
                Console.WriteLine("  pr-builds         Print builds for a given pull request");
                Console.WriteLine("  tests             Print build test failures");
                Console.WriteLine("  helix             Print helix logs for build");
                Console.WriteLine("  helix-jobs        Print helix jobs for builds");
                Console.WriteLine("  search-timeline   Search timeline info");
                Console.WriteLine("  search-helix      Search helix logs");
                Console.WriteLine("  search-buildlog   Search build logs");
                Console.WriteLine("  timeline          Dump the timeline");
                Console.WriteLine("  yaml              Dump the YML for a build");
                Console.WriteLine("  clear-cache       Clear out the cache");
                Console.WriteLine();
                Console.WriteLine("=== Global Options ===");
                optionSet.WriteOptionDescriptions(Console.Out);
            }

            /*
            static GitHubClient CreateGitHubClient()
            {
                var gitHubClient = new GitHubClient(new ProductHeaderValue("runfo-app"));
                var value = Environment.GetEnvironmentVariable("RUNFO_GITHUB_TOKEN");
                if (value is object)
                {
                    var both = value.Split(new[] { ':' }, count: 2);
                    gitHubClient.Credentials = new Credentials(both[0], both[1]);
                }

                return gitHubClient;
            }
            */
        }

        // TODO: need to make this usable by others
        internal static async Task<string?> GetPersonalAccessTokenFromFile(string name)
        {
            try
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

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
