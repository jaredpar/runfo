using Azure.Storage.Blobs;
using Newtonsoft.Json;
using Octokit;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Math.EC.Rfc7748;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace DevOps.Util.DotNet
{
    public sealed class BlobStorageUtil : IAzureStorageUtil
    {
        private sealed class TimelineStorage
        {
            public int LatestIndex { get; set;  }
            public int LatestAttempt { get; set;  }
            public List<Timeline>? Timelines { get; set; }
        }

        public BlobContainerClient TimelineContainerClient { get; }
        public BlobContainerClient TestRunsContainerClient { get; }
        public BlobContainerClient TestResultsContainerClient { get; }

        public string Organization { get; }

        public BlobStorageUtil(string organization, string connectionString)
        {
            Organization = organization;
            TimelineContainerClient = new BlobContainerClient(connectionString, "timelines");
            TestRunsContainerClient = new BlobContainerClient(connectionString, "testruns");
            TestResultsContainerClient = new BlobContainerClient(connectionString, "testresults");
        }

        public string GetBlobName(string project, int buildNumber) =>
            $"{Organization}-{project}-{buildNumber}.json";

        public string GetBlobName(string project, int testRunId, TestOutcome[]? outcomes)
        {
            var o = "none";
            if (outcomes is object)
            {
                o = string.Join('-', outcomes.Select(x => x.ToString()));
            }

            return $"{Organization}-{project}-{testRunId}-{o}.json";
        }

        public async Task<Timeline> GetTimelineAttemptAsync(string project, int buildNumber, int attempt, CancellationToken cancellationToken = default)
        {
            var timelineStorage = await GetTimelineStorageAsync(project, buildNumber, cancellationToken).ConfigureAwait(false);
            var timelineAttempt = timelineStorage.Timelines.First(x => x.GetAttempt() == attempt);
            if (timelineAttempt is null)
            {
                throw new InvalidOperationException($"Could not find timeline attempt {attempt}");
            }

            return timelineAttempt;
        }

        public async Task<Timeline> GetTimelineAsync(string project, int buildNumber, CancellationToken cancellationToken = default)
        {
            var timelineStorage = await GetTimelineStorageAsync(project, buildNumber, cancellationToken).ConfigureAwait(false);
            return timelineStorage.Timelines![timelineStorage.LatestIndex];
        }

        private async Task<TimelineStorage> GetTimelineStorageAsync(string project, int buildNumber, CancellationToken cancellationToken) 
        {
            var blobName = GetBlobName(project, buildNumber);
            return await LoadJsonAsync<TimelineStorage>(TimelineContainerClient, blobName, cancellationToken).ConfigureAwait(false);
        }

        public async Task SaveTimelineAsync(string project, int buildNumber, List<Timeline> timelineList, CancellationToken cancellationToken = default)
        {
            var latestIndex = 0;
            var latestAttempt = 0;
            for (int i =0; i < timelineList.Count; i++)
            {
                var attempt = timelineList[i].GetAttempt();
                if (attempt > latestAttempt)
                {
                    latestIndex = i;
                    latestAttempt = attempt;
                }
            }

            var timelineStorage = new TimelineStorage()
            {
                LatestIndex = latestIndex,
                LatestAttempt = latestAttempt,
                Timelines = timelineList,
            };

            var blobName = GetBlobName(project, buildNumber);
            await SaveJsonAsync(TimelineContainerClient, blobName, timelineStorage, cancellationToken);
        }

        public async Task SaveTestRunsAsync(string project, int buildNumber, List<TestRun> testRunList, CancellationToken cancellationToken = default)
        {
            if (testRunList.Count > 100)
            {
                // Aribtary limit
                return;
            }

            var blobName = GetBlobName(project, buildNumber);
            await SaveJsonAsync(TestRunsContainerClient, blobName, testRunList.ToArray(), cancellationToken).ConfigureAwait(false);
        }

        public async Task<List<TestRun>> ListTestRunsAsync(string project, int buildNumber, CancellationToken cancellationToken = default)
        {
            var blobName = GetBlobName(project, buildNumber);
            var array = await LoadJsonAsync<TestRun[]>(TestRunsContainerClient, blobName, cancellationToken).ConfigureAwait(false);
            return new List<TestRun>(array);
        }

        public async Task SaveTestResultsAsync(string project, int testRunId, TestOutcome[]? outcomes, List<TestCaseResult> testResults, CancellationToken cancellationToken = default)
        {
            if (testResults.Count > 200)
            {
                // Aribtrary limit
                return;
            }

            if (outcomes is null || outcomes.Any(x => !DotNetUtil.FailedTestOutcomes.Contains(x)))
            {
                // Only store failed outcems
                return;
            }

            var blobName = GetBlobName(project, testRunId, outcomes);
            await SaveJsonAsync(TestResultsContainerClient, blobName, testResults.ToArray(), cancellationToken);
        }

        public async Task<List<TestCaseResult>> ListTestResultsAsync(string project, int testRunId, TestOutcome[]? outcomes = null, CancellationToken cancellationToken = default)
        {
            var blobName = GetBlobName(project, testRunId, outcomes);
            var array = await LoadJsonAsync<TestCaseResult[]>(TestResultsContainerClient, blobName, cancellationToken).ConfigureAwait(false);
            return new List<TestCaseResult>(array);
        }

        private static async Task<T> LoadJsonAsync<T>(BlobContainerClient containerClient, string blobName, CancellationToken cancellationToken)
        {
            var blobClient = containerClient.GetBlobClient(blobName);
            var response = await blobClient.DownloadAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(response.Value.Content, Encoding.UTF8);
            var json = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<T>(json);
        }

        private static async Task SaveJsonAsync<T>(BlobContainerClient containerClient, string blobName, T value, CancellationToken cancellationToken)
        {
            var json = JsonConvert.SerializeObject(value);
            var blobClient = containerClient.GetBlobClient(blobName);

            var bytes = Encoding.UTF8.GetBytes(json);
            using var memoryStream = new MemoryStream(bytes);
            memoryStream.Position = 0;
            await blobClient.UploadAsync(
                memoryStream,
                overwrite: true,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
