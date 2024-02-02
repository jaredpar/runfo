﻿using System;
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
            var settingsData = await SettingsData.ReadAsync();
            var disableCache = false;
            var optionSet = new OptionSet()
            {
                { "azdo-token=", "The Azure DevOps personal access token", t => settingsData.AzureToken = t },
                { "helix-token=", "The helix personal access token", ht => settingsData.HelixToken = ht},
                { "helix-base-uri=", "The helix base URI, defaults to production: https://helix.dot.net/", hb => settingsData.HelixBaseUri = hb.EndsWith("/") ? hb : hb + "/"},
                { "dc|disable-cache", "Disable caching", dc => disableCache = dc is object }
            };

            try
            {
                args = optionSet.Parse(args).ToArray();

                if (string.IsNullOrEmpty(settingsData.AzureToken))
                {
                    Console.WriteLine("No Azure DevOps token found, use `runfo settings` to set one");
                }

                var devopsServer = new DevOpsServer(DotNetUtil.AzureOrganization, new AuthorizationToken(AuthorizationKind.PersonalAccessToken, settingsData.AzureToken!));
                var azureUtil = new CachingAzureUtil(
                    new LocalAzureStorageUtil(DotNetUtil.AzureOrganization, RuntimeInfoUtil.CacheDirectory),
                    new AzureUtil(devopsServer));

                var runtimeInfo = new RuntimeInfo(devopsServer, new HelixServer(settingsData.HelixBaseUri, settingsData.HelixToken), azureUtil);

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
                    case "helix":
                        return await runtimeInfo.PrintHelix(commandArgs);
                    case "helix-jobs":
                        return await runtimeInfo.PrintHelixJobs(commandArgs);
                    case "get-helix-payload":
                        return await runtimeInfo.GetHelixPayload(commandArgs);
                    case "search-buildlog":
                        return await runtimeInfo.PrintSearchBuildLogs(commandArgs);
                    case "search-tests":
                        return await runtimeInfo.PrintFailedTests(commandArgs);
                    case "search-timeline":
                        return await runtimeInfo.PrintSearchTimeline(commandArgs);
                    case "search-helix":
                        return await runtimeInfo.PrintSearchHelix(commandArgs);
                    case "settings":
                        return await runtimeInfo.RunSettingsAsync();
                    case "timeline":
                        return await runtimeInfo.PrintTimeline(commandArgs);
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
                Console.WriteLine("  artifacts          Print artifact info for a given build");
                Console.WriteLine("  builds             Print builds");
                Console.WriteLine("  clear-cache        Clear out the cache");
                Console.WriteLine("  definitions        Print build definition info");
                Console.WriteLine("  get-helix-payload  Download helix payload for a given job and workitems");
                Console.WriteLine("  helix              Print helix logs for build");
                Console.WriteLine("  helix-jobs         Print helix jobs for builds");
                Console.WriteLine("  pr-builds          Print builds for a given pull request");
                Console.WriteLine("  status             Print build definition status");
                Console.WriteLine("  search-timeline    Search timeline info");
                Console.WriteLine("  search-helix       Search helix logs");
                Console.WriteLine("  search-buildlog    Search build logs");
                Console.WriteLine("  search-tests       Print build test failures");
                Console.WriteLine("  timeline           Dump the timeline");
                Console.WriteLine("  yaml               Dump the YML for a build");
                Console.WriteLine();
                Console.WriteLine("=== Global Options ===");
                optionSet.WriteOptionDescriptions(Console.Out);
            }
        }
    }
}
