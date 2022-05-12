using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.Client.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DevOps.Util
{
    public sealed class HelixServer
    {
        private readonly DevOpsHttpClient _client;
        private readonly string? _token;
        private readonly string? _helixBaseUri;

        public IHelixApi HelixApi => GetHelixApi(_helixBaseUri, _token);

        public HelixServer(string? helixBaseUri = null, string? token = null, HttpClient? httpClient = null)
        {
            _helixBaseUri = helixBaseUri;
            _token = token;
            _client = new DevOpsHttpClient(httpClient: httpClient);
        }

        public static IHelixApi GetHelixApi(string? helixBaseUri = null, string? token = null)
        {
            helixBaseUri = string.IsNullOrEmpty(helixBaseUri) ? "https://helix.dot.net/" : helixBaseUri;
            if (string.IsNullOrEmpty(token))
            {
                return ApiFactory.GetAnonymous(helixBaseUri);
            }

            var authToken = new AuthorizationToken(AuthorizationKind.PersonalAccessToken, token);
            return ApiFactory.GetAuthenticated(helixBaseUri, authToken.Token);
        }

        public async ValueTask GetHelixPayloads(string jobId, List<string> workItems, string downloadDir, bool ignoreDumps)
        {
            if (!Path.IsPathFullyQualified(downloadDir))
            {
                downloadDir = Path.Combine(Environment.CurrentDirectory, downloadDir);
            }

            IHelixApi helixApi = HelixApi;
            JobDetails jobDetails = await helixApi.Job.DetailsAsync(jobId).ConfigureAwait(false);
            string? jobListFile = jobDetails.JobList;

            if (string.IsNullOrEmpty(jobListFile))
            {
                throw new ArgumentException($"Couldn't find job list for job {jobId}, if it is an internal job, please use a helix access token from {_helixBaseUri}Account/Tokens");
            }

            using MemoryStream memoryStream = await _client.DownloadFileAsync(jobListFile).ConfigureAwait(false);
            using StreamReader reader = new StreamReader(memoryStream);
            string jobListJson = await reader.ReadToEndAsync().ConfigureAwait(false);

            WorkItemInfo[] workItemsInfo = JsonConvert.DeserializeObject<WorkItemInfo[]>(jobListJson);
            
            Directory.CreateDirectory(downloadDir);
            string jobListSaveFile = Path.Combine(downloadDir, "joblist.json");

            Console.WriteLine($"Job List => {jobListSaveFile}");
            File.WriteAllText(jobListSaveFile, jobListJson);

            if (workItemsInfo.Length > 0)
            {
                string correlationDir = Path.Combine(downloadDir, "correlation-payload");
                Directory.CreateDirectory(correlationDir);

                // download correlation payload
                JObject correlationPayload = workItemsInfo[0].CorrelationPayloadUrisWithDestinations ?? new JObject();
                foreach (JProperty property in correlationPayload.Children())
                {
                    string url = property.Name;
                    Uri uri = new Uri(url);
                    string fileName = uri.Segments[^1];
                    string destinationFile = Path.Combine(correlationDir, fileName);
                    Console.WriteLine($"Payload {fileName} => {destinationFile}");
                    await _client.DownloadZipFileAsync(url, destinationFile, showProgress: true, writer: Console.Out).ConfigureAwait(false);
                }

                string workItemsDir = Path.Combine(downloadDir, "workitems");
                Directory.CreateDirectory(workItemsDir);

                bool downloadAll = false;
                bool downloadFirst = workItems.Count == 0;

                if (!downloadFirst && workItems[0] == "all")
                {
                    downloadAll = true;
                }

                foreach (WorkItemInfo workItemInfo in workItemsInfo)
                {
                    if (!downloadFirst && !downloadAll && !workItems.Contains(workItemInfo.WorkItemId ?? string.Empty))
                        continue;

                    if (string.IsNullOrEmpty(workItemInfo.PayloadUri))
                        continue;

                    await DownloadWorkitemFiles(workItemInfo.WorkItemId!, workItemInfo.PayloadUri).ConfigureAwait(false);

                    // if no workitems specified, download the first one,
                    // usefull to download any workitem and inspect the payload structure for debugging
                    if (downloadFirst)
                        return;
                }

                async Task DownloadWorkitemFiles(string workItemId, string payloadUri)
                {
                    string itemDir = Path.Combine(workItemsDir, workItemId);
                    Directory.CreateDirectory(itemDir);

                    Console.WriteLine();
                    Console.WriteLine($"------ Downloading files for: {workItemId} -------");
                    Console.WriteLine();

                    string fileName = Path.Combine(workItemsDir, itemDir, $"{workItemId}.zip");

                    Console.WriteLine($"WorkItem {workItemId} => {fileName}");

                    await _client.DownloadZipFileAsync(payloadUri, fileName, showProgress: true, writer: Console.Out).ConfigureAwait(false);

                    IEnumerable<UploadedFile> workitemFiles = await helixApi.WorkItem.ListFilesAsync(workItemId, jobId);
                    foreach (var file in workitemFiles)
                    {
                        if (ignoreDumps && (file.Name.StartsWith("core.", StringComparison.OrdinalIgnoreCase) || file.Name.EndsWith(".dmp", StringComparison.OrdinalIgnoreCase)))
                            continue;

                        string destFile = Path.Combine(itemDir, file.Name);
                        string? dstDirName = Path.GetDirectoryName(destFile);
                        if (dstDirName != null && !Directory.Exists(dstDirName))
                        {
                            Directory.CreateDirectory(dstDirName);
                        }

                        Console.WriteLine($"{file.Name} => {destFile}");
                        await _client.DownloadFileAsync(file.Link, destFile, showProgress: true, writer: Console.Out).ConfigureAwait(false);
                    }
                }
            }
        }

        public async Task<List<(string Name, string Link)>> GetUploadedFilesAsync(string jobId, string workItemId, CancellationToken cancellationToken = default)
        {
            IHelixApi helixApi = HelixApi;
            var files = await helixApi.WorkItem.ListFilesAsync(workItemId, job: jobId, cancellationToken).ConfigureAwait(false);
            return files
                .Select(x => (x.Name, x.Link))
                .ToList();
        }

        public Task<MemoryStream> DownloadFileAsync(string uri) => _client.DownloadFileAsync(uri);

        private class WorkItemInfo
        {
            public JObject? CorrelationPayloadUrisWithDestinations { get; set; }
            public string? PayloadUri { get; set; }
            public string? WorkItemId { get; set; }
        }
    }
}
