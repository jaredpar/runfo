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
        public LocalAzureStorageUtil LocalAzureStorageUtil { get; }
        public IAzureUtil BackupAzureUtil { get; }

        public string Organization => LocalAzureStorageUtil.Organization;

        public CachingAzureUtil(LocalAzureStorageUtil localAzureStorageUtil, DevOpsServer server)
            : this(localAzureStorageUtil, new AzureUtil(server))
        {

        }

        public CachingAzureUtil(LocalAzureStorageUtil localAzureStorageUtil, IAzureUtil backupAzureUtil)
        {
            if (localAzureStorageUtil.Organization != backupAzureUtil.Organization)
            {
                throw new ArgumentException($"Organization values {localAzureStorageUtil.Organization} and {backupAzureUtil.Organization} do not match");
            }

            LocalAzureStorageUtil = localAzureStorageUtil;
            BackupAzureUtil = backupAzureUtil;
        }

        public async Task<Timeline> GetTimelineAttemptAsync(string project, int buildNumber, int attempt, CancellationToken cancellationToken = default)
        {
            Timeline timeline;
            try
            {
                timeline = await LocalAzureStorageUtil.GetTimelineAttemptAsync(project, buildNumber, attempt, cancellationToken).ConfigureAwait(false);
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
                timeline = await LocalAzureStorageUtil.GetTimelineAsync(project, buildNumber, cancellationToken).ConfigureAwait(false);
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
                return await LocalAzureStorageUtil.ListTestRunsAsync(project, buildNumber, cancellationToken).ConfigureAwait(false);
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
                return await LocalAzureStorageUtil.ListTestResultsAsync(project, testRunId, outcomes, cancellationToken).ConfigureAwait(false);
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
            await LocalAzureStorageUtil.SaveTestRunsAsync(project, buildNumber, list, cancellationToken).ConfigureAwait(false);
            return list;
        }

        private async Task<List<Timeline>> ListAndCacheTimelinesAsync(string project, int buildNumber, CancellationToken cancellationToken)
        {
            var list = await BackupAzureUtil.ListTimelineAttemptsAsync(project, buildNumber).ConfigureAwait(false);
            await LocalAzureStorageUtil.SaveTimelineAsync(project, buildNumber, list, cancellationToken).ConfigureAwait(false);
            return list;
        }

        private async Task<List<TestCaseResult>> ListAndCacheTestResultsAsync(string project, int testRunId, TestOutcome[]? outcomes, CancellationToken cancellationToken)
        {
            var list = await BackupAzureUtil.ListTestResultsAsync(project, testRunId, outcomes).ConfigureAwait(false);
            await LocalAzureStorageUtil.SaveTestResultsAsync(project, testRunId, outcomes, list, cancellationToken).ConfigureAwait(false);
            return list;
        }
    }
}
