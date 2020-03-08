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

namespace DevOps.Functions
{
    public static class Functions
    {
        [FunctionName("build")]
        public static async Task<IActionResult> OnBuild(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger logger,
            [Queue("build-complete", Connection = "AzureWebJobsStorage")] IAsyncCollector<string> queueCollector)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync().ConfigureAwait(false);
            var connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING");
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            int id = data.resource.id;
            await queueCollector.AddAsync(id.ToString()).ConfigureAwait(false);

            using var generalUtil = new GeneralUtil(connectionString, logger);
            await generalUtil.UploadBuildEventAsync(id, requestBody).ConfigureAwait(false);
            return new OkResult();
        }

        [FunctionName("build-upload")]
        public static async Task OnBuildComplete(
            [QueueTrigger("build-complete", Connection = "AzureWebJobsStorage")] string message,
            ILogger logger)
        {
            var buildId = int.Parse(message);
            logger.LogInformation($"Processing build {buildId}");
            var connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING");
            using var cloneTimeUtil = new CloneTimeUtil(connectionString, logger);
            if (await cloneTimeUtil.IsBuildUploadedAsync(buildId))
            {
                logger.LogInformation($"Build {buildId} is already uploaded");
                return;
            }
            await cloneTimeUtil.UploadBuildAsync(buildId);
        }
    }
}
