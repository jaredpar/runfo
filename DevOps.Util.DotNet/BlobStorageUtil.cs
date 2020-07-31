using Azure.Storage.Blobs;
using Newtonsoft.Json;
using Octokit;
using Org.BouncyCastle.Math.EC.Rfc7748;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace DevOps.Util.DotNet
{
    public sealed class BlobStorageUtil
    {
        private sealed class TimelineStorage
        {
            public int LatestIndex { get; set;  }
            public int LatestAttempt { get; set;  }
            public List<Timeline>? Timelines { get; set; }
        }

        public BlobContainerClient BlobContainerClient { get; }

        public BlobStorageUtil(string connectionString)
        {
            BlobContainerClient = new BlobContainerClient(connectionString, "timelines");
        }

        public string GetBlobName(string organization, string project, int buildNumber) =>
            $"{organization}-{project}-{buildNumber}.json";

        public Task<Timeline> GetTimelineAttemptAsync(string organization, string project, int buildNumber, int? attempt, CancellationToken cancellationToken = default) =>
            attempt is { } n
            ? GetTimelineAttemptAsync(organization, project, buildNumber, n, cancellationToken)
            : GetTimelineAsync(organization, project, buildNumber, cancellationToken);

        public async Task<Timeline> GetTimelineAttemptAsync(string organization, string project, int buildNumber, int attempt, CancellationToken cancellationToken = default)
        {
            var timelineStorage = await GetTimelineStorageAsync(organization, project, buildNumber, cancellationToken).ConfigureAwait(false);
            var timelineAttempt = timelineStorage.Timelines.First(x => x.GetAttempt() == attempt);
            if (timelineAttempt is null)
            {
                throw new InvalidOperationException($"Could not find timeline attempt {attempt}");
            }

            return timelineAttempt;
        }

        public async Task<Timeline> GetTimelineAsync(string organization, string project, int buildNumber, CancellationToken cancellationToken = default)
        {
            var timelineStorage = await GetTimelineStorageAsync(organization, project, buildNumber, cancellationToken).ConfigureAwait(false);
            return timelineStorage.Timelines![timelineStorage.LatestIndex];
        }

        private async Task<TimelineStorage> GetTimelineStorageAsync(string organization, string project, int buildNumber, CancellationToken cancellationToken = default)
        {
            var blobName = GetBlobName(organization, project, buildNumber);
            var blobClient = BlobContainerClient.GetBlobClient(blobName);
            var response = await blobClient.DownloadAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(response.Value.Content, Encoding.UTF8);
            var json = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<TimelineStorage>(json);
        }

        public async Task SaveTimelineAsync(string organization, string project, int buildNumber, List<Timeline> timelineList, CancellationToken cancellationToken = default)
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

            var json = JsonConvert.SerializeObject(timelineStorage);
            var blobName = $"{organization}-{project}-{buildNumber}.json";
            var blobClient = BlobContainerClient.GetBlobClient(blobName);

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
