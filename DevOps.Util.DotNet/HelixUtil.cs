
using DevOps.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
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

        public static async Task<MemoryStream> GetHelixAttachmentContentAsync(
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
        public static async Task<HelixLogInfo> GetHelixLogInfoAsync(Stream resultsStream)
        {
            string consoleUri = null;
            string coreUri = null;
            string testResultsUri = null;

            using var reader = new StreamReader(resultsStream);
            string line = await reader.ReadLineAsync();
            while (line is object)
            {
                if (Regex.IsMatch(line, @"console.*\.log:"))
                {
                    consoleUri = (await reader.ReadLineAsync()).Trim();
                }
                else if (Regex.IsMatch(line, @"core\..*:"))
                {
                    coreUri = (await reader.ReadLineAsync()).Trim();
                }
                else if (Regex.IsMatch(line, @"testResults.*xml:"))
                {
                    testResultsUri = (await reader.ReadLineAsync()).Trim();
                }

                line = await reader.ReadLineAsync();
            }

            return new HelixLogInfo(
                consoleUri: consoleUri,
                coreDumpUri: coreUri,
                testResultsUri: testResultsUri);
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

            return await GetHelixLogInfoAsync(stream);
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
    }
}