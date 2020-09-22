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
using DevOps.Util.DotNet.Triage;
using Octokit;
using DevOps.Util;
using System;
using Microsoft.Extensions.Primitives;
using System.Net.Http;
using System.Dynamic;
using System.Net;
using System.Net.Http.Formatting;
using static DevOps.Util.DotNet.DotNetConstants;
using System.Diagnostics;

namespace DevOps.Functions
{
    public class BuildInfoMessage
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
        public GitHubClientFactory GitHubClientFactory { get; }
        public Functions(DevOpsServer server, TriageContext context, GitHubClientFactory gitHubClientFactory)
        {
            Server = server;
            Context = context;
            TriageContextUtil = new TriageContextUtil(context);
            GitHubClientFactory = gitHubClientFactory;
        }

        [FunctionName("status")]
        public async Task<IActionResult> OnStatusAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger logger)
        {
            dynamic status = new ExpandoObject();
            try
            {
                _ = await GitHubClientFactory.CreateForAppAsync("dotnet", "runtime");
                status.CreatedGitHubApp = true;
            }
            catch (Exception ex)
            {
                status.CreatedGitHubApp = false;
                status.CreatedGitHubAppException = ex.Message;
            }

            return new JsonResult((ExpandoObject)status);
        }

        /// <summary>
        /// This is the web hook for the AzDO instance when a build completes
        /// </summary>
        [FunctionName("build")]
        public async Task<IActionResult> OnBuild(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            [Queue("build-complete", Connection = ConfigurationAzureBlobConnectionString)] IAsyncCollector<string> completeCollector,
            ILogger logger)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync().ConfigureAwait(false);
            logger.LogInformation(requestBody);

            dynamic data = JsonConvert.DeserializeObject(requestBody);
            var message = new BuildInfoMessage()
            {
                BuildNumber = data.resource.id,
                ProjectId = data.resourceContainers.project.id
            };

            await completeCollector.AddAsync(JsonConvert.SerializeObject(message));
            return new OkResult();
        }

        /// <summary>
        /// This function is called once a build completes. The point of this function is to save the build data 
        /// into the SQL DB.
        /// </summary>
        [FunctionName("build-complete")]
        public async Task BuildCompleteAsync(
            [QueueTrigger("build-complete", Connection = ConfigurationAzureBlobConnectionString)] string message,
            [Queue("build-triage", Connection = ConfigurationAzureBlobConnectionString)] IAsyncCollector<string> triageCollector,
            [Queue("osx-retry", Connection = ConfigurationAzureBlobConnectionString)] IAsyncCollector<string> retryCollector,
            ILogger logger)
        {
            var buildInfoMessage = JsonConvert.DeserializeObject<BuildInfoMessage>(message);
            var projectName = buildInfoMessage.ProjectName;
            if (projectName is null)
            {
                var projectId = buildInfoMessage.ProjectId;
                if (projectId is null)
                {
                    logger.LogError("Both project name and id are null");
                    return;
                }

                buildInfoMessage.ProjectName = await Server.ConvertProjectIdToNameAsync(projectId);
            }

            logger.LogInformation($"Gathering data for build {buildInfoMessage.ProjectName} {buildInfoMessage.BuildNumber}");
            var build = await Server.GetBuildAsync(buildInfoMessage.ProjectName!, buildInfoMessage.BuildNumber);
            var queryUtil = new DotNetQueryUtil(Server);
            var modelDataUtil = new ModelDataUtil(queryUtil, TriageContextUtil, logger);
            await modelDataUtil.EnsureModelInfoAsync(build);

            await triageCollector.AddAsync(JsonConvert.SerializeObject(buildInfoMessage));
            await retryCollector.AddAsync(JsonConvert.SerializeObject(buildInfoMessage));
        }

        /// <summary>
        /// This function will see if the build matches any active issues that we are tracking. By the time this 
        /// message is hit the build attempt should be fully saved to our SQL DB
        /// </summary>
        [FunctionName("build-triage")]
        public async Task BuildTriageAsync(
            [QueueTrigger("build-triage", Connection = ConfigurationAzureBlobConnectionString)] string message,
            ILogger logger)
        {
            var buildInfoMessage = JsonConvert.DeserializeObject<BuildInfoMessage>(message);
            Debug.Assert(buildInfoMessage.ProjectName is object);

            logger.LogInformation($"Triaging build {buildInfoMessage.ProjectName} {buildInfoMessage.BuildNumber}");

            var util = new LegacyAutoTriageUtil(Server, Context, logger);
            await util.TriageBuildAsync(buildInfoMessage.ProjectName, buildInfoMessage.BuildNumber);
        }

        [FunctionName("osx-retry")]
        public async Task RetryMac(
            [QueueTrigger("osx-retry", Connection = ConfigurationAzureBlobConnectionString)] string message,
            ILogger logger)
        {
            var buildInfoMessage = JsonConvert.DeserializeObject<BuildInfoMessage>(message);
            Debug.Assert(buildInfoMessage.ProjectName is object);
            var util = new LegacyAutoTriageUtil(Server, Context, logger);
            await util.RetryOsxDeprovisionAsync(buildInfoMessage.ProjectName, buildInfoMessage.BuildNumber);
        }

        [FunctionName("issues-update")]
        public async Task IssuesUpdate(
            [TimerTrigger("0 */15 15-23 * * 1-5")] TimerInfo timerInfo,
            ILogger logger)
        { 
            var util = new LegacyTriageGitHubUtil(GitHubClientFactory, Context, logger);
            await util.UpdateGithubIssues();
            await util.UpdateStatusIssue();
        }

        [FunctionName("webhook-github")]
        public async Task<IActionResult> OnGitHubEvent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest request,
            [Queue("pull-request-merged", Connection = ConfigurationAzureBlobConnectionString)] IAsyncCollector<string> collector,
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
            [QueueTrigger("pull-request-merged", Connection = ConfigurationAzureBlobConnectionString)] string message,
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

    }
}
