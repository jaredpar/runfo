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
using Microsoft.Extensions.Primitives;

namespace DevOps.Functions
{
    public class BuildCompleteMessage
    {
        public string? ProjectId { get; set; }

        public string? ProjectName { get; set; }

        public int BuildNumber { get; set; }
    }

    public class PullRequestMergedMessage
    {
        public string? Organization { get; set; }
        public string? Repository { get; set; }
        public int PullRequestNumber { get; set; }
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

        [FunctionName("webhook-github")]
        public async Task<IActionResult> OnGitHubEvent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest request,
            [Queue("pull-request-merged", Connection = "AzureWebJobsStorage")] IAsyncCollector<string> collector,
            ILogger logger)
        {
            request.Headers.TryGetValue("X-GitHub-Event", out StringValues eventName);
            logger.LogInformation(eventName);
            if (eventName == "pull_request")
            {
                string requestBody = await new StreamReader(request.Body).ReadToEndAsync().ConfigureAwait(false);
                dynamic prInfo = JsonConvert.DeserializeObject(requestBody);
                if (prInfo.action == "closed" &&
                    prInfo.pull_request != null &&
                    prInfo.pull_request.merged == true)
                {

                    try
                    {
                        string fullName = prInfo.repository.full_name;
                        var both = fullName.Split("/");
                        var organization = both[0];
                        var repository = both[1];
                        var message = new PullRequestMergedMessage()
                        {
                            Organization = organization,
                            Repository = repository,
                            PullRequestNumber = prInfo.pull_request.number
                        };

                        await collector.AddAsync(JsonConvert.SerializeObject(message));
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Error reading pull request info: " + ex.Message);
                        throw;
                    }
                }
            }

            return new OkResult();
        }

        [FunctionName("pull-request-merged")]
        public async Task OnPullRequestMergedAsync(
            [QueueTrigger("pull-request-merged", Connection = "AzureWebJobsStorage")] string message,
            ILogger logger)
        {
            var functionUtil = new FunctionUtil();
            var prMessage = JsonConvert.DeserializeObject<PullRequestMergedMessage>(message);
            var prKey = new GitHubPullRequestKey(prMessage.Organization!, prMessage.Repository!, prMessage.PullRequestNumber);
            await functionUtil.OnPullRequestMergedAsync(
                Server,
                TriageContextUtil,
                prKey,
                DotNetUtil.DefaultAzureProject);
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
