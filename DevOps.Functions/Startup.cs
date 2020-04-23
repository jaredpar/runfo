using System;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Octokit;

[assembly: FunctionsStartup(typeof(DevOps.Functions.Startup))]

namespace DevOps.Functions
{
    internal class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var connectionString = Environment.GetEnvironmentVariable("RUNFO_CONNECTION_STRING");
            var azdoToken = Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN");
            builder.Services.AddDbContext<TriageContext>(options => options.UseSqlServer(connectionString));
            builder.Services.AddScoped<DevOpsServer>(_ => new DevOpsServer(DotNetUtil.Organization, azdoToken));
            builder.Services.AddScoped<GitHubClient>(_ => new GitHubClient(new ProductHeaderValue("RuntimeStatusPage")));
        }
    }
}