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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mono.Options;
using Octokit;
using static DevOps.Util.DotNet.OptionSetUtil;

[assembly: Microsoft.Extensions.Configuration.UserSecrets.UserSecretsId("8c127652-56b4-4501-9323-d1f40a41c512")]

internal class Program
{
    internal const int ExitSuccess = 0;
    internal const int ExitFailure = 1;

    public static async Task Main(string[] args) => await MainCore(CreateTriageContext(), args.ToList());

    private static TriageContext CreateTriageContext()
    {
        var builder = new DbContextOptionsBuilder<TriageContext>();
        ConfigureOptions(builder, forceDev: true);
        return new TriageContext(builder.Options);
    }

    private static void ConfigureOptions(DbContextOptionsBuilder builder, bool forceDev = false)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

        if (!string.IsNullOrEmpty(config["RUNFO_DEV"]) || forceDev)
        {
            Console.WriteLine("using sql dev");
            var connectionString = config["RUNFO_CONNECTION_STRING_DEV"];
            builder.UseSqlServer(connectionString);
        }
        else
        {
            Console.WriteLine("using sql");
            var connectionString = config["RUNFO_CONNECTION_STRING"];
            builder.UseSqlServer(connectionString);
        }
    }

    // This entry point exists so that `dotnet ef database` and `migrations` has an 
    // entry point to create TriageDbContext
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddDbContext<TriageContext>(options => ConfigureOptions(options));
            });

    // internal static async Task<int> Main(string[] args) => await MainCore(args.ToList());

    internal static async Task<int> MainCore(TriageContext context, List<string> args)
    {
        var azdoToken = Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN");
        var gitHubToken = Environment.GetEnvironmentVariable("RUNFO_GITHUB_TOKEN");
        var cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "runfo", "json");

        var server = new CachingDevOpsServer(cacheDirectory, "dnceng", azdoToken);
        var gitHubClient = new GitHubClient(new ProductHeaderValue("RuntimeStatusPage"));
        var queryUtil = new DotNetQueryUtil(server);
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        // TODO: should not hard code jaredpar here
        gitHubClient.Credentials = new Credentials("jaredpar", gitHubToken);

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

        var autoTriageUtil = new AutoTriageUtil(server, gitHubClient, context, loggerFactory.CreateLogger<AutoTriageUtil>());
        var gitHubUtil = new GitHubUtil(gitHubClient, context, loggerFactory.CreateLogger<GitHubClient>());
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
            autoTriageUtil.EnsureTriageIssues();
            await autoTriageUtil.Triage("-d runtime -c 100 -pr");
            await autoTriageUtil.Triage("-d runtime-official -c 20 -pr");
            await gitHubUtil.UpdateGithubIssues();
            await gitHubUtil.UpdateStatusIssue();
        }

        async Task RunIssues()
        {
            await gitHubUtil.UpdateGithubIssues();
            await gitHubUtil.UpdateStatusIssue();
        }

        async Task RunRebuild()
        {
            autoTriageUtil.EnsureTriageIssues();
            await autoTriageUtil.Triage("-d runtime -c 1000 -pr");
            await autoTriageUtil.Triage("-d aspnet -c 1000 -pr");
            await autoTriageUtil.Triage("-d runtime-official -c 50 -pr");
            await autoTriageUtil.Triage("-d aspnet-official -c 50 -pr");
        }

        async Task RunScratch()
        {
            autoTriageUtil.EnsureTriageIssues();
            autoTriageUtil.UpdateIssues = false;
            //await autoTriageUtil.Triage("-d runtime -c 100 -pr");
            await gitHubUtil.UpdateGithubIssues();
            await gitHubUtil.UpdateStatusIssue();
        }
    }
}