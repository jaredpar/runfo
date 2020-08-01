using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet
{
    public interface IAzureUtil
    {
        string Organization { get; }

        Task<Timeline> GetTimelineAttemptAsync(string project, int buildNumber, int? attempt, CancellationToken cancellationToken = default) =>
            attempt is { } a
            ? GetTimelineAttemptAsync(project, buildNumber, a, cancellationToken)
            : GetTimelineAsync(project, buildNumber, cancellationToken);

        Task<Timeline> GetTimelineAttemptAsync(string project, int buildNumber, int attempt, CancellationToken cancellationToken = default);

        Task<Timeline> GetTimelineAsync(string project, int buildNumber, CancellationToken cancellationToken = default);

        async Task<List<Timeline>> ListTimelineAttemptsAsync(string project, int buildNumber)
        {
            var list = new List<Timeline>();

            var timeline = await GetTimelineAsync(project, buildNumber).ConfigureAwait(false);
            if (timeline is null)
            {
                return list;
            }

            list.Add(timeline);

            // Special case the easy case here
            if (timeline.Records.All(x => x.Attempt == 1))
            {
                return list;
            }

            var attempts = timeline
                .Records
                .SelectMany(x => x.PreviousAttempts ?? Array.Empty<TimelineAttempt>())
                .Select(x => x.Attempt)
                .Distinct()
                .OrderBy(x => x);
            foreach (var attempt in attempts)
            {
                var attemptTimeline = await this.GetTimelineAttemptAsync(project, buildNumber, attempt).ConfigureAwait(false);
                if (attemptTimeline is object)
                {
                    list.Add(attemptTimeline);
                }
            }

            return list;
        }
    }

    public interface IAzureStorageUtil : IAzureUtil
    {
        Task SaveTimelineAsync(string project, int buildNumber, List<Timeline> timelineList, CancellationToken cancellationToken = default);
    }

    public sealed class AzureUtil : IAzureUtil
    {
        public DevOpsServer DevOpsServer { get; }

        public string Organization => DevOpsServer.Organization;

        public AzureUtil(DevOpsServer server)
        {
            DevOpsServer = server;
        }

        public Task<Timeline> GetTimelineAttemptAsync(string project, int buildNumber, int attempt, CancellationToken cancellationToken = default) =>
            DevOpsServer.GetTimelineAttemptAsync(project, buildNumber, attempt);


        public Task<Timeline> GetTimelineAsync(string project, int buildNumber, CancellationToken cancellationToken = default) =>
            DevOpsServer.GetTimelineAsync(project, buildNumber);

        public Task<List<Timeline>> ListTimelineAttemptsAsync(string project, int buildNumber)
        {
            IAzureUtil azureUtil = this;
            return azureUtil.ListTimelineAttemptsAsync(project, buildNumber);
        }
    }

    public sealed class CachingAzureUtil : IAzureUtil
    {
        public IAzureStorageUtil MainAzureStorageUtil { get; }
        public IAzureUtil BackupAzureUtil { get; }

        public string Organization => MainAzureStorageUtil.Organization;

        public CachingAzureUtil(IAzureStorageUtil mainAzureStorageUtil, DevOpsServer server)
            : this(mainAzureStorageUtil, new AzureUtil(server))
        {

        }

        public CachingAzureUtil(IAzureStorageUtil mainAzureStorageUtil, IAzureUtil backupAzureUtil)
        {
            if (mainAzureStorageUtil.Organization != backupAzureUtil.Organization)
            {
                throw new ArgumentException($"Organization values {mainAzureStorageUtil.Organization} and {backupAzureUtil.Organization} do not match");
            }

            MainAzureStorageUtil = mainAzureStorageUtil;
            BackupAzureUtil = backupAzureUtil;
        }

        public async Task<Timeline> GetTimelineAttemptAsync(string project, int buildNumber, int attempt, CancellationToken cancellationToken = default)
        {
            Timeline timeline;
            try
            {
                timeline = await MainAzureStorageUtil.GetTimelineAttemptAsync(project, buildNumber, attempt, cancellationToken).ConfigureAwait(false);
                if (timeline is object)
                {
                    return timeline;
                }
            }
            catch (Exception)
            {
                // Go to backup on error
            }

            var list = await ListAndCacheTimelines(project, buildNumber, cancellationToken).ConfigureAwait(false);
            return list.First(x => x.GetAttempt() == attempt);
        }

        public async Task<Timeline> GetTimelineAsync(string project, int buildNumber, CancellationToken cancellationToken = default)
        {
            Timeline timeline;
            try
            {
                timeline = await MainAzureStorageUtil.GetTimelineAsync(project, buildNumber, cancellationToken).ConfigureAwait(false);
                if (timeline is object)
                {
                    return timeline;
                }
            }
            catch (Exception)
            {
                // Go to backup on error
            }

            var list = await ListAndCacheTimelines(project, buildNumber, cancellationToken).ConfigureAwait(false);
            return list
                .OrderByDescending(x => x.GetAttempt())
                .First();
        }

        private async Task<List<Timeline>> ListAndCacheTimelines(string project, int buildNumber, CancellationToken cancellationToken)
        {
            var list = await BackupAzureUtil.ListTimelineAttemptsAsync(project, buildNumber).ConfigureAwait(false);
            await MainAzureStorageUtil.SaveTimelineAsync(project, buildNumber, list, cancellationToken).ConfigureAwait(false);
            return list;
        }
    }
}
