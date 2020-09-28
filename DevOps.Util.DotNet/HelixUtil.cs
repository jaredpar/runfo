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

namespace DevOps.Util.DotNet
{
    public static class HelixUtil
    {
        public static bool IsHelixWorkItem(TestCaseResult testCaseResult) =>
            TryGetHelixInfo(testCaseResult) is HelixInfo info &&
            testCaseResult.TestCaseTitle == $"{info.WorkItemName} Work Item";

        public static bool IsHelixTestCaseResult(TestCaseResult testCaseResult) =>
            TryGetHelixInfo(testCaseResult) is HelixInfo info &&
            testCaseResult.TestCaseTitle != $"{info.WorkItemName} Work Item";

        public static bool IsHelixWorkItemAndTestCaseResult(TestCaseResult workItem, TestCaseResult test) =>
            IsHelixWorkItem(workItem) &&
            !IsHelixWorkItem(test) &&
            TryGetHelixInfo(workItem) is HelixInfo left &&
            TryGetHelixInfo(test) is HelixInfo right &&
            left == right;

        public static HelixInfo? TryGetHelixInfo(TestCaseResult testCaseResult)
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
                    return new HelixInfo(jobId: jobId, workItemName: workItemName);
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
        /// Parse out the UploadFileResults file to get the console and core URIs
        /// </summary>
        private static async Task<HelixLogInfo> GetHelixLogInfoAsync(string? runClientUri, Stream resultsStream)
        {
            string? consoleUri = null;
            string? coreUri = null;
            string? testResultsUri = null;

            using var reader = new StreamReader(resultsStream);
            string? line = await reader.ReadLineAsync();
            while (line is object)
            {
                if (Regex.IsMatch(line, @"console.*\.log:"))
                {
                    consoleUri = (await reader.ReadLineAsync())?.Trim();
                }
                else if (Regex.IsMatch(line, @"core\..*:"))
                {
                    coreUri = (await reader.ReadLineAsync())?.Trim();
                }
                else if (Regex.IsMatch(line, @"testResults.*xml:"))
                {
                    testResultsUri = (await reader.ReadLineAsync())?.Trim();
                }

                line = await reader.ReadLineAsync();
            }

            return new HelixLogInfo(
                runClientUri: RewriteUri(runClientUri),
                consoleUri: RewriteUri(consoleUri),
                coreDumpUri: RewriteUri(coreUri),
                testResultsUri: RewriteUri(testResultsUri));

            // This works around the following arcade bug which causes query strings to be imporperly escaped
            // https://github.com/dotnet/arcade/issues/6256
            static string? RewriteUri(string? uri)
            {
                if (uri is object && uri.Contains(':') && Uri.TryCreate(uri, UriKind.Absolute, out var realUri))
                {
                    var builder = new UriBuilder(realUri);
                    builder.Query = Uri.EscapeDataString(realUri.Query);
                    return builder.Uri.ToString();
                }

                return uri;
            }
        }

        public static async Task<HelixLogInfo> GetHelixLogInfoAsync(
            DevOpsServer server,
            HelixWorkItem workItem)
        {
            using var stream = await GetHelixAttachmentContentAsync(server, workItem.ProjectName, workItem.TestRun.Id, workItem.TestCaseResult.Id);
            if (stream is null)
            {
                return HelixLogInfo.Empty;
            }

            var runClientUri = await GetRunClientUri(server, workItem.HelixInfo);
            return await GetHelixLogInfoAsync(runClientUri, stream);
        }

        public static async Task<string> GetHelixConsoleText(
            DevOpsServer server,
            string consoleUri)
        {
            try
            {
                using var stream = await server.DownloadFileAsync(consoleUri);
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static async Task<string?> GetRunClientUri(
            DevOpsServer server,
            HelixInfo helixInfo)
        {
            try
            {
                var uri = $"https://helix.dot.net/api/2019-06-17/jobs/{helixInfo.JobId}/workitems/{helixInfo.WorkItemName}/";
                var json = await server.GetJsonAsync(uri).ConfigureAwait(false);
                if (json is null)
                {
                    return null;
                }

                dynamic d = JObject.Parse(json);
                foreach (dynamic? log in d.Logs)
                {
                    if (log is object && log.Module == "run_client.py")
                    {
                        return log!.Uri;
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}