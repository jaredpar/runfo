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
using static DevOps.Util.DotNet.Function.FunctionConstants;
using System.Diagnostics;
using DevOps.Util.DotNet.Function;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace DevOps.Functions
{
    public class Functions
    {
        public TriageContext Context { get; }
        public TriageContextUtil TriageContextUtil { get; }
        public DevOpsServer Server { get; }
        public HelixServer HelixServer { get; }
        public IGitHubClientFactory GitHubClientFactory { get; }
        public SiteLinkUtil SiteLinkUtil { get; }

        public Functions(DevOpsServer server, TriageContext context, IGitHubClientFactory gitHubClientFactory)
        {
            Server = server;
            Context = context;
            TriageContextUtil = new TriageContextUtil(context);
            GitHubClientFactory = gitHubClientFactory;
            SiteLinkUtil = SiteLinkUtil.Published;
            HelixServer = new HelixServer();
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
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Queue(QueueNameBuildComplete, Connection = ConfigurationAzureBlobConnectionString)] IAsyncCollector<string> completeCollector,
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
            [QueueTrigger(QueueNameBuildComplete, Connection = ConfigurationAzureBlobConnectionString)] string message,
            [Queue(QueueNameTriageBuild, Connection = ConfigurationAzureBlobConnectionString)] IAsyncCollector<string> triageCollector,
            [Queue(QueueNameBuildRetry, Connection = ConfigurationAzureBlobConnectionString)] IAsyncCollector<string> retryCollector,
            ILogger logger)
        {
            var buildInfoMessage = JsonConvert.DeserializeObject<BuildInfoMessage>(message);
            var projectName = buildInfoMessage.ProjectName;
            var buildNumber = buildInfoMessage.BuildNumber;
            if (projectName is null)
            {
                var projectId = buildInfoMessage.ProjectId;
                if (projectId is null)
                {
                    logger.LogError("Both project name and id are null");
                    return;
                }

                projectName = await Server.ConvertProjectIdToNameAsync(projectId);
            }

            logger.LogInformation($"Gathering data for build {projectName} {buildNumber}");
            if (projectName != "public")
            {
                logger.LogError($"Asked to gather data from {projectName} which is not 'public'");
                return;
            }

            var build = await Server.GetBuildAsync(projectName, buildNumber);
            var queryUtil = new DotNetQueryUtil(Server);
            var modelDataUtil = new ModelDataUtil(queryUtil, TriageContextUtil, logger);
            var buildAttemptKey = await modelDataUtil.EnsureModelInfoAsync(build);

            await triageCollector.AddAsync(JsonConvert.SerializeObject(new BuildMessage(buildAttemptKey.BuildKey)));
            await retryCollector.AddAsync(JsonConvert.SerializeObject(new BuildAttemptMessage(buildAttemptKey)));
        }

        [FunctionName("build-retry")]
        public async Task BuildRetryAsync(
            [QueueTrigger(QueueNameBuildRetry, Connection = ConfigurationAzureBlobConnectionString)] string message,
            ILogger logger)
        {
            var buildAttemptMessage = JsonConvert.DeserializeObject<BuildAttemptMessage>(message);
            if (buildAttemptMessage.BuildAttemptKey is { } buildAttemptKey)
            {
                var util = new BuildRetryUtil(Server, Context, logger);
                await util.ProcessBuildAsync(buildAttemptKey.BuildKey);
            }
            else
            {
                logger.LogError($"Message not a valid build attempt key: {message}");
            }
        }

        /// <summary>
        /// This function will triage a tracking issue against a build (all attempts)
        /// </summary>
        [FunctionName("triage-tracking-issue")]
        public async Task TriageTrackingIssueAsync(
            [QueueTrigger(QueueNameTriageTrackingIssue, Connection = ConfigurationAzureBlobConnectionString)] string message,
            ILogger logger)
        {
            var issueMessage = JsonConvert.DeserializeObject<TriageTrackingIssueMessage>(message);
            if (issueMessage.BuildMessage?.BuildKey is { } buildKey && issueMessage.ModelTrackingIssueId is { } trackingIssueId)
            {
                logger.LogInformation($"Triaging issue {trackingIssueId} against build {buildKey}");
                var queryUtil = new DotNetQueryUtil(Server);
                var util = new TrackingIssueUtil(HelixServer, queryUtil, TriageContextUtil, logger);
                await util.TriageAsync(buildKey, trackingIssueId);
            }
            else
            {
                logger.LogError($"Message not a valid build attempt key: {message}");
            }
        }

        /// <summary>
        /// This function will triage a tracking issue against a build range (all attempts)
        /// </summary>
        [FunctionName("triage-tracking-issue-range")]
        public async Task TriageTrackingIssueRangeAsync(
            [QueueTrigger(QueueNameTriageTrackingIssueRange, Connection = ConfigurationAzureBlobConnectionString)] string message,
            [Queue(QueueNameTriageTrackingIssue, Connection = ConfigurationAzureBlobConnectionString)] IAsyncCollector<string> triageCollector,
            ILogger logger)
        {
            var rangeMessage = JsonConvert.DeserializeObject<TriageTrackingIssueRangeMessage>(message);
            if (rangeMessage.ModelTrackingIssueId is { } issueId && rangeMessage.BuildMessages is { } buildMessages)
            {
                var list = new List<Task>();
                foreach (var buildMessage in buildMessages)
                {
                    var triageMessage = new TriageTrackingIssueMessage()
                    {
                        ModelTrackingIssueId = issueId,
                        BuildMessage = buildMessage,
                    };
                    var text = JsonConvert.SerializeObject(triageMessage);
                    list.Add(triageCollector.AddAsync(text));
                }

                await Task.WhenAll(list);
            }
            else
            {
                logger.LogError($"Invalid message: {message}");
            }
        }

        /// <summary>
        /// This will schedule the build to be triaged against all of the active tracking issues
        /// definition
        /// </summary>
        [FunctionName("triage-build")]
        public async Task TriageBuildAsync(
            [QueueTrigger(QueueNameTriageBuild, Connection = ConfigurationAzureBlobConnectionString)] string message,
            [Queue(QueueNameTriageTrackingIssue, Connection = ConfigurationAzureBlobConnectionString)] IAsyncCollector<string> triageCollector,
            ILogger logger)
        {
            var buildMessage = JsonConvert.DeserializeObject<BuildMessage>(message);
            if (buildMessage.BuildKey is { } buildKey)
            {
                logger.LogInformation($"Triaging build: {buildKey}");

                var ids = await Context.ModelTrackingIssues.Where(x => x.IsActive).Select(x => x.Id).ToListAsync();
                foreach (var id in ids)
                {
                    var triageMessage = new TriageTrackingIssueMessage(buildKey, id);
                    var text = JsonConvert.SerializeObject(triageMessage);
                    await triageCollector.AddAsync(text);
                }
            }
            else
            {
                logger.LogError($"Message not a valid build attempt key: {message}");
            }
        }

        [FunctionName("issues-update-timer")]
        public async Task IssuesUpdateTimerAsync(
            [TimerTrigger("0 */15 15-23 * * 1-5")] TimerInfo timerInfo,
            ILogger logger)
        {
            var util = new TrackingGitHubUtil(GitHubClientFactory, Context, SiteLinkUtil, logger);
            await util.UpdateTrackingGitHubIssuesAsync();
        }

        [FunctionName("issues-update-status-page")]
        public async Task IssuesUpdateStatusPageAsync(
            [TimerTrigger("0 */30 15-23 * * 1-5")] TimerInfo timerInfo,
            ILogger logger)
        {
            var util = new StatusPageUtil(GitHubClientFactory, Context, logger);
            await util.UpdateStatusIssue();
        }

        [FunctionName("issues-update-manual")]
        public async Task IssuesUpdateManualAsync(
            [QueueTrigger(QueueNameIssueUpdateManual, Connection = ConfigurationAzureBlobConnectionString)] string message,
            ILogger logger)
        { 
            var updateMessage = JsonConvert.DeserializeObject<IssueUpdateManualMessage>(message);
            if (updateMessage.ModelTrackingIssueId is { } id)
            {
                var util = new TrackingGitHubUtil(GitHubClientFactory, Context, SiteLinkUtil, logger);
                await util.UpdateTrackingGitHubIssueAsync(id);
            }
            else
            {
                logger.LogError($"Message not a valid update message: {message}");
            }
        }

        [FunctionName("webhook-github")]
        public async Task<IActionResult> OnGitHubEvent(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest request,
            [Queue(QueueNamePullRequestMerged, Connection = ConfigurationAzureBlobConnectionString)] IAsyncCollector<string> collector,
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
            [QueueTrigger(QueueNamePullRequestMerged, Connection = ConfigurationAzureBlobConnectionString)] string message,
            ILogger logger)
        {
            var functionUtil = new FunctionUtil(logger);
            var prMessage = JsonConvert.DeserializeObject<PullRequestMergedMessage>(message);
            var prKey = new GitHubPullRequestKey(prMessage.Organization!, prMessage.Repository!, prMessage.PullRequestNumber);
            await functionUtil.OnPullRequestMergedAsync(
                Server,
                TriageContextUtil,
                prKey,
                DotNetConstants.DefaultAzureProject);
        }

        [FunctionName("delete-old-builds")]
        public async Task DeleteOldBuilds(
            [TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo,
            ILogger logger)
        {
            var functionUtil = new FunctionUtil(logger);
            await functionUtil.DeleteOldBuilds(TriageContextUtil.Context, deleteMax: 25);
        }
    }
}
