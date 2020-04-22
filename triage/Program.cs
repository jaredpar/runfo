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
using DevOps.Util.Triage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mono.Options;
using Octokit;
using static DevOps.Util.DotNet.OptionSetUtil;

internal static class Program
{
    internal const int ExitSuccess = 0;
    internal const int ExitFailure = 1;

    public static void Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        var db = host.Services.GetService<TriageDbContext>();
        using (var scope = host.Services.CreateScope())
        {
            db = host.Services.GetService<TriageDbContext>();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddDbContext<TriageDbContext>(options =>
                    options.UseSqlite(@"Data Source=C:\Users\jaredpar\AppData\Local\runfo\triage.db"));
            });

    // internal static async Task<int> Main(string[] args) => await MainCore(args.ToList());

    internal static async Task<int> MainCore(List<string> args)
    {
        var azdoToken = Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN");
        var gitHubToken = Environment.GetEnvironmentVariable("RUNFO_GITHUB_TOKEN");
        var cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "runfo", "json");

        var server = new CachingDevOpsServer(cacheDirectory, "dnceng", azdoToken);
        var gitHubClient = new GitHubClient(new ProductHeaderValue("RuntimeStatusPage"));
        var queryUtil = new DotNetQueryUtil(server);

        // TODO: should not hard code jaredpar here
        gitHubClient.Credentials = new Credentials("jaredpar", gitHubToken);

        using var triageUtil = new TriageUtil();
        string command;
        if (args.Count == 0)
        {
            command = "list";
        }
        else
        {
            command = args[0];
            args = args.Skip(1).ToList();
        }

        switch (command)
        {
            case "auto":
                await RunAutoTriage(args);
                break;
            case "issues":
                await RunIssues();
                break;
            case "rebuild":
                await RunRebuild();
                break;
            case "scratch":
                await RunScratch();
                break;
            default:
                Console.WriteLine($"Unrecognized option {command}");
                break;
        }

        return ExitSuccess;

        async Task RunAutoTriage(List<string> args)
        {
            using var autoTriageUtil = new AutoTriageUtil(server, gitHubClient);
            await autoTriageUtil.Triage("-d runtime -c 100 -pr");
            await autoTriageUtil.Triage("-d runtime-official -c 20 -pr");
            await autoTriageUtil.UpdateQueryIssues();
            await autoTriageUtil.UpdateStatusIssue();
        }

        async Task RunIssues()
        {
            using var autoTriageUtil = new AutoTriageUtil(server, gitHubClient);
            await autoTriageUtil.UpdateQueryIssues();
            await autoTriageUtil.UpdateStatusIssue();
        }

        async Task RunRebuild()
        {
            using var autoTriageUtil = new AutoTriageUtil(server, gitHubClient);
            autoTriageUtil.EnsureTriageIssues();
            await autoTriageUtil.Triage("-d runtime -c 500 -pr");
            await autoTriageUtil.Triage("-d aspnet -c 300 -pr");
            await autoTriageUtil.Triage("-d runtime-official -c 50 -pr");
        }

        async Task RunScratch()
        {
            using var autoTriageUtil = new AutoTriageUtil(server, gitHubClient);
            // autoTriageUtil.EnsureTriageIssues();
            // await autoTriageUtil.Triage("-d runtime -c 500 -pr");
            // await autoTriageUtil.Triage("-d runtime-official -c 50 -pr");
            // await autoTriageUtil.UpdateQueryIssues();
            await autoTriageUtil.UpdateStatusIssue();
        }
    }
}