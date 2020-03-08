using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Util;
using Mono.Options;
using static RuntimeInfoUtil;

public class Program
{
    internal static async Task<int> Main(string[] args)
    {
        try
        {
            var runtimeInfo = new RuntimeInfo(await GetPersonalAccessToken());
            if (args.Length == 0)
            {
                await runtimeInfo.PrintBuildResults(Array.Empty<string>());
                return ExitSuccess;
            }

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
                case "builds":
                    return await runtimeInfo.PrintBuilds(commandArgs);
                case "pr-builds":
                    return await runtimeInfo.PrintPullRequestBuilds(commandArgs);
                case "tests":
                    return await runtimeInfo.PrintFailedTests(commandArgs);
                case "helix":
                    return await runtimeInfo.PrintHelix(commandArgs);
                case "timeline":
                    return await runtimeInfo.PrintTimeline(commandArgs);
                case "search-timeline":
                    return await runtimeInfo.PrintSearchTimeline(commandArgs);
                case "search-helix":
                    return await runtimeInfo.PrintSearchHelix(commandArgs);
                default:
                    Console.WriteLine($"Error: {command} is not recognized as a valid command");
                    ShowHelp();
                    return ExitFailure;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return ExitFailure;
        }

        static void ShowHelp()
        {
            Console.WriteLine("runfo");
            Console.WriteLine("\tstatus\t\tPrint build definition status");
            Console.WriteLine("\tdefinitions\tPrint build definition info");
            Console.WriteLine("\tbuilds\t\tPrint builds");
            Console.WriteLine("\tpr-builds\t\tPrint builds for a given pull request");
            Console.WriteLine("\ttests\t\tPrint build test failures");
            Console.WriteLine("\thelix\t\tPrint helix logs for build");
            Console.WriteLine("\tsearch-timeline\t\tSerach timeline info");
            Console.WriteLine("\tsearch-helix\t\tSerach helix logs");
            Console.WriteLine("\ttimeline\t\tdump the timeline");
        }

        async Task<string> GetPersonalAccessToken()
        { 
            string token = Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN");
            var optionSet = new OptionSet()
            {
                { "t|token=", "The Azure DevOps personal access token", t => token = t },
            };

            args = optionSet.Parse(args).ToArray();
            if (token is null)
            {
                token = await GetPersonalAccessTokenFromFile();
            }
            return token;
        }
    }

    // TODO: need to make this usable by others
    private static async Task<string> GetPersonalAccessTokenFromFile()
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(@"p:\tokens.txt");
            foreach (var line in lines)
            {
                var split = line.Split(':', count: 2);
                if ("dnceng" == split[0])
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
