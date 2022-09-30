using DevOps.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Helix.Client;

namespace DevOps.Util.DotNet
{
    public static class HelixUtil
    {
        public static bool IsHelixWorkItem(TestCaseResult testCaseResult) =>
            TryGetHelixWorkItem(testCaseResult) is HelixInfoWorkItem info &&
            testCaseResult.TestCaseTitle == $"{info.WorkItemName} Work Item";

        public static bool IsHelixTestCaseResult(TestCaseResult testCaseResult) =>
            TryGetHelixWorkItem(testCaseResult) is HelixInfoWorkItem info &&
            testCaseResult.TestCaseTitle != $"{info.WorkItemName} Work Item";

        public static bool IsHelixWorkItemAndTestCaseResult(TestCaseResult workItem, TestCaseResult test) =>
            IsHelixWorkItem(workItem) &&
            !IsHelixWorkItem(test) &&
            TryGetHelixWorkItem(workItem) is HelixInfoWorkItem left &&
            TryGetHelixWorkItem(test) is HelixInfoWorkItem right &&
            left == right;

        public static HelixInfoWorkItem? TryGetHelixWorkItem(TestCaseResult testCaseResult)
        {
            try
            {
                if (testCaseResult.Comment is null)
                {
                    return null;
                }

                dynamic obj = JObject.Parse(testCaseResult.Comment);
                var jobId = (string)obj.HelixJobId;
                var workItemName = (string)obj.HelixWorkItemName;
                if (!string.IsNullOrEmpty(jobId) && !string.IsNullOrEmpty(workItemName))
                {
                    return new HelixInfoWorkItem(JobId: jobId, WorkItemName: workItemName);
                }
            }
            catch
            {

            }

            return null;
        }

        public static async Task<MemoryStream?> GetHelixAttachmentContentAsync(
            DevOpsServer server,
            string project,
            int runId,
            int testCaseResultId)
        {
            var attachments = await server.GetTestCaseResultAttachmentsAsync(project, runId, testCaseResultId);
            var attachment = attachments.FirstOrDefault(x => x.FileName == "UploadFileResults.txt");
            if (attachment is null)
            {
                return null;
            }

            return await server.DownloadTestCaseResultAttachmentZipAsync(project, runId, testCaseResultId, attachment.Id);
        }

        /// <summary>
        /// This works around the following arcade bug which causes query strings to be imporperly escaped
        /// https://github.com/dotnet/arcade/issues/6256
        /// </summary>
        internal static string? RewriteUri(string? uri)
        {
            if (uri is object && uri.Contains(':') && Uri.TryCreate(uri, UriKind.Absolute, out var realUri))
            {
                var builder = new UriBuilder(realUri);
                builder.Query = Uri.EscapeDataString(realUri.Query);
                return builder.Uri.ToString();
            }

            return uri;
        }

        public static async Task<HelixLogInfo> GetHelixLogInfoAsync(
            IHelixApi helixApi,
            HelixInfoWorkItem helixWorkItem)
        {
            var details = await helixApi.WorkItem.DetailsExAsync(id: helixWorkItem.WorkItemName, job: helixWorkItem.JobId).ConfigureAwait(false);
            var runClientUri = details.Logs.FirstOrDefault(x => x.Module.StartsWith("run_client"))?.Uri;
            string? dumpUri = null;
            string? testResultsUri = null;

            foreach (var file in details.Files)
            {
                // TODO: Helix can upload multiple dump files but at the moment we only support 
                // one of the API in our info type here. Need to adjust that. For now just grab the 
                // first
                if (dumpUri is null)
                {
                    if (file.FileName.StartsWith("core") || file.FileName.EndsWith(".dmp"))
                    {
                        dumpUri = file.Uri;
                    }
                }

                if (file.FileName.EndsWith(".xml"))
                {
                    testResultsUri = file.Uri;
                }
            }

            return new HelixLogInfo(
                runClientUri: runClientUri,
                consoleUri: details.ConsoleOutputUri,
                coreDumpUri: dumpUri,
                testResultsUri: testResultsUri);
        }

        public static async Task<string> GetHelixConsoleText(
            IHelixApi helixApi,
            HelixInfoWorkItem helixWorkItem)
        {
            try
            {
                using var stream = await helixApi.WorkItem.ConsoleLogAsync(id: helixWorkItem.WorkItemName, job: helixWorkItem.JobId).ConfigureAwait(false);
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}