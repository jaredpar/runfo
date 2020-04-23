using System;
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

namespace DevOps.Functions
{
    public class BuildCompleteMessage
    {
        public string ProjectId { get; set; }
        public int BuildNumber { get; set; }
    }

    public class Functions
    {
        public TriageContext Context { get; }

        public TriageContextUtil TriageContextUtil { get; }

        public DevOpsServer Server { get; }

        public GitHubClient GitHubClient { get; }

        public Functions(DevOpsServer server, GitHubClient gitHubClient, TriageContext context)
        {
            Server = server;
            GitHubClient = gitHubClient;
            Context = context;
            TriageContextUtil = new TriageContextUtil(context);
        }

        [FunctionName("build")]
        public async Task<IActionResult> OnBuild(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger logger,
            [Queue("build-complete", Connection = "AzureWebJobsStorage")] IAsyncCollector<string> queueCollector)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync().ConfigureAwait(false);
            logger.LogInformation(requestBody);

            dynamic data = JsonConvert.DeserializeObject(requestBody);
            var message = new BuildCompleteMessage()
            {
                BuildNumber = data.resource.id,
                ProjectId = data.resourceContainers.project.id
            };

            await queueCollector.AddAsync(JsonConvert.SerializeObject(message));
            return new OkResult();
        }

        [FunctionName("triage-build")]
        public async Task OnBuildComplete(
            [QueueTrigger("build-complete", Connection = "AzureWebJobsStorage")] string message,
            ILogger logger)
        {
            var buildCompleteMessage = JsonConvert.DeserializeObject<BuildCompleteMessage>(message);
            var projectName = await Server.ConvertProjectIdToNameAsync(buildCompleteMessage.ProjectId);

            logger.LogInformation($"Triaging build {projectName} {buildCompleteMessage.BuildNumber}");

            var util = new AutoTriageUtil(Server, GitHubClient, Context, logger);
            await util.Triage(projectName, buildCompleteMessage.BuildNumber);
        }

        [FunctionName("issues-update")]
        public async Task IssuesUpdate(
            [TimerTrigger("0 */15 * * * *")] TimerInfo timerInfo,
            ILogger logger)
        {
            var util = new AutoTriageUtil(Server, GitHubClient, Context, logger);
            await util.UpdateQueryIssues();
            await util.UpdateStatusIssue();
        }
    }
}
