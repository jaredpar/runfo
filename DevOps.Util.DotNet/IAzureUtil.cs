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
}
