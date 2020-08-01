using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

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

        Task<List<TestRun>> ListTestRunsAsync(string project, int buildNumber, CancellationToken cancellationToken = default);

        Task<List<TestCaseResult>> ListTestResultsAsync(string project, int testRunId, TestOutcome[]? outcomes = null, CancellationToken cancellationToken = default);

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
        Task SaveTestRunsAsync(string project, int buildNumber, List<TestRun> testRunList, CancellationToken cancellationToken = default);
        Task SaveTestResultsAsync(string project, int testRunId, TestOutcome[]? outcomes, List<TestCaseResult> testResults, CancellationToken cancellationToken = default);
    }

    public sealed class AzureUtil : IAzureUtil
    {
        public DevOpsServer DevOpsServer { get; }

        public string Organization => DevOpsServer.Organization;

        public AzureUtil(DevOpsServer server)
        {
            DevOpsServer = server;
        }

        public async Task<Timeline> GetTimelineAttemptAsync(string project, int buildNumber, int attempt, CancellationToken cancellationToken = default)
        {
            var timeline = await DevOpsServer.GetTimelineAttemptAsync(project, buildNumber, attempt).ConfigureAwait(false);
            if (timeline is null)
            {
                throw new InvalidOperationException();
            }

            return timeline;
        }

        public async Task<Timeline> GetTimelineAsync(string project, int buildNumber, CancellationToken cancellationToken = default)
        {
            var timeline = await DevOpsServer.GetTimelineAsync(project, buildNumber).ConfigureAwait(false);
            if (timeline is null)
            {
                throw new InvalidOperationException();
            }

            return timeline;
        }

        public Task<List<Timeline>> ListTimelineAttemptsAsync(string project, int buildNumber)
        {
            IAzureUtil azureUtil = this;
            return azureUtil.ListTimelineAttemptsAsync(project, buildNumber);
        }

        public async Task<List<TestRun>> ListTestRunsAsync(string project, int buildNumber, CancellationToken cancellationToken = default)
        {
            var runs = await DevOpsServer.ListTestRunsAsync(project, buildNumber).ConfigureAwait(false);
            return new List<TestRun>(runs);
        }

        public async Task<List<TestCaseResult>> ListTestResultsAsync(string project, int testRunId, TestOutcome[]? outcomes = null, CancellationToken cancellationToken = default)
        {
            var testResults = await DevOpsServer.ListTestResultsAsync(project, testRunId, outcomes).ConfigureAwait(false);
            return testResults;
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

            var list = await ListAndCacheTimelinesAsync(project, buildNumber, cancellationToken).ConfigureAwait(false);
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

            var list = await ListAndCacheTimelinesAsync(project, buildNumber, cancellationToken).ConfigureAwait(false);
            return list
                .OrderByDescending(x => x.GetAttempt())
                .First();
        }

        public async Task<List<TestRun>> ListTestRunsAsync(string project, int buildNumber, CancellationToken cancellationToken = default)
        {
            try
            {
                return await MainAzureStorageUtil.ListTestRunsAsync(project, buildNumber, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Go to backup
            }

            return await ListAndCacheTestRunsAsync(project, buildNumber, cancellationToken).ConfigureAwait(false);
        }


        public async Task<List<TestCaseResult>> ListTestResultsAsync(string project, int testRunId, TestOutcome[]? outcomes = null, CancellationToken cancellationToken = default)
        {
            try
            {
                return await MainAzureStorageUtil.ListTestResultsAsync(project, testRunId, outcomes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Go to backup
            }

            return await ListAndCacheTestResultsAsync(project, testRunId, outcomes, cancellationToken).ConfigureAwait(false);
        }

        private async Task<List<TestRun>> ListAndCacheTestRunsAsync(string project, int buildNumber, CancellationToken cancellationToken)
        {
            var list = await BackupAzureUtil.ListTestRunsAsync(project, buildNumber, cancellationToken).ConfigureAwait(false);
            await MainAzureStorageUtil.SaveTestRunsAsync(project, buildNumber, list, cancellationToken).ConfigureAwait(false);
            return list;
        }

        private async Task<List<Timeline>> ListAndCacheTimelinesAsync(string project, int buildNumber, CancellationToken cancellationToken)
        {
            var list = await BackupAzureUtil.ListTimelineAttemptsAsync(project, buildNumber).ConfigureAwait(false);
            await MainAzureStorageUtil.SaveTimelineAsync(project, buildNumber, list, cancellationToken).ConfigureAwait(false);
            return list;
        }

        private async Task<List<TestCaseResult>> ListAndCacheTestResultsAsync(string project, int testRunId, TestOutcome[]? outcomes, CancellationToken cancellationToken)
        {
            var list = await BackupAzureUtil.ListTestResultsAsync(project, testRunId, outcomes).ConfigureAwait(false);
            await MainAzureStorageUtil.SaveTestResultsAsync(project, testRunId, outcomes, list, cancellationToken).ConfigureAwait(false);
            return list;
        }
    }
}
