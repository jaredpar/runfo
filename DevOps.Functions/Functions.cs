#nullable enable

using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Configuration;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Octokit;
using DevOps.Util;
using System;

namespace DevOps.Functions
{
    public class BuildCompleteMessage
    {
        public string? ProjectId { get; set; }

        public string? ProjectName { get; set; }

        public int BuildNumber { get; set; }
    }

    public class Functions
    {
        public TriageContext Context { get; }

        public TriageContextUtil TriageContextUtil { get; }

        public DevOpsServer Server { get; }

        public IGitHubClient GitHubClient { get; }

        public Functions(DevOpsServer server, IGitHubClient gitHubClient, TriageContext context)
        {
            Server = server;
            GitHubClient = gitHubClient;
            Context = context;
            TriageContextUtil = new TriageContextUtil(context);
        }

        [FunctionName("build")]
        public async Task<IActionResult> OnBuild(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            [Queue("build-complete", Connection = "AzureWebJobsStorage")] IAsyncCollector<string> completeCollector,
            [Queue("osx-retry", Connection = "AzureWebJobsStorage")] IAsyncCollector<string> retryCollector,
            ILogger logger)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync().ConfigureAwait(false);
            logger.LogInformation(requestBody);

            dynamic data = JsonConvert.DeserializeObject(requestBody);
            var message = new BuildCompleteMessage()
            {
                BuildNumber = data.resource.id,
                ProjectId = data.resourceContainers.project.id
            };

            await completeCollector.AddAsync(JsonConvert.SerializeObject(message));
            await retryCollector.AddAsync(JsonConvert.SerializeObject(message));
            return new OkResult();
        }

        [FunctionName("triage-build")]
        public async Task TriageBuildAsync(
            [QueueTrigger("build-complete", Connection = "AzureWebJobsStorage")] string message,
            ILogger logger)
        {
            var buildCompleteMessage = JsonConvert.DeserializeObject<BuildCompleteMessage>(message);
            var projectName = buildCompleteMessage.ProjectName;
            if (projectName is null)
            {
                var projectId = buildCompleteMessage.ProjectId;
                if (projectId is null)
                {
                    logger.LogError("Both project name and id are null");
                    return;
                }

                projectName = await Server.ConvertProjectIdToNameAsync(projectId);
            }

            logger.LogInformation($"Triaging build {projectName} {buildCompleteMessage.BuildNumber}");

            var util = new AutoTriageUtil(Server, Context, GitHubClient, logger);
            await util.TriageBuildAsync(projectName, buildCompleteMessage.BuildNumber);
        }

        [FunctionName("triage-query")]
        public async Task TriageQueryAsync(
            [QueueTrigger("triage-query", Connection = "AzureWebJobsStorage")] string message,
            [Queue("build-complete", Connection = "AzureWebJobsStorage")] IAsyncCollector<BuildCompleteMessage> triageQueue,
            ILogger logger)
        {
            logger.LogInformation($"Triaging query: {message}");
            var queryUtil = new DotNetQueryUtil(Server, GitHubClient);
            foreach (var build in await queryUtil.ListBuildsAsync(message))
            {
                var key = build.GetBuildKey();
                var buildCompleteMessage = new BuildCompleteMessage()
                {
                    ProjectName = key.Project,
                    BuildNumber = key.Number,
                };
                await triageQueue.AddAsync(buildCompleteMessage);
            }
        }

        [FunctionName("issues-update")]
        public async Task IssuesUpdate(
            [TimerTrigger("0 */15 15-23 * * 1-5")] TimerInfo timerInfo,
            ILogger logger)
        { 
            var util = new TriageGitHubUtil(GitHubClient, Context, logger);
            await util.UpdateGithubIssues();
            await util.UpdateStatusIssue();
        }

        [FunctionName("osx-retry")]
        public async Task RetryMac(
            [QueueTrigger("osx-retry", Connection = "AzureWebJobsStorage")] string message,
            ILogger logger)
        {
            var buildCompleteMessage = JsonConvert.DeserializeObject<BuildCompleteMessage>(message);
            var projectId = buildCompleteMessage.ProjectId ?? buildCompleteMessage.ProjectName;
            if (projectId is null)
            {
                logger.LogError("Both project name and id are null");
                return;
            }

            var projectName = await Server.ConvertProjectIdToNameAsync(projectId);
            var util = new AutoTriageUtil(Server, Context, GitHubClient, logger);
            await util.RetryOsxDeprovisionAsync(projectName, buildCompleteMessage.BuildNumber);
        }
    }
}
