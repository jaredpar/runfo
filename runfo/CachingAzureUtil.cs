using DevOps.Util;
using DevOps.Util.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Runfo
{
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
