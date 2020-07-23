#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace DevOps.Util
{
    public static class DevOpsUtilExtensions
    {
        public static IEnumerable<T> SelectNotNull<T>(this IEnumerable<T?> enumerable)
            where T : class
        {
            foreach (var current in enumerable)
            {
                if (current is object)
                {
                    yield return current;
                }
            }
        }

        public static IEnumerable<T> SelectNullableValue<T>(this IEnumerable<T?> enumerable)
            where T : struct
        {
            foreach (var current in enumerable)
            {
                if (current.HasValue)
                {
                    yield return current.Value;
                }
            }
        }

        public static IEnumerable<U> SelectNullableValue<T, U>(this IEnumerable<T> enumerable, Func<T, U?> func)
            where U : struct =>
            enumerable
                .Select(func)
                .SelectNullableValue();

        public static async Task<T?> FirstOrDefaultAsync<T>(this IAsyncEnumerable<T> enumerable)
            where T : class
        {
            await foreach (var current in enumerable.ConfigureAwait(false))
            {
                return current;
            }

            return default;
        }

        public static BuildKey GetBuildKey(this Build build) => DevOpsUtil.GetBuildKey(build);

        public static BuildInfo GetBuildInfo(this Build build) => DevOpsUtil.GetBuildInfo(build);

        public static BuildDefinitionInfo GetBuildDefinitionInfo(this Build build) => DevOpsUtil.GetBuildDefinitionInfo(build);

        public static DateTimeOffset? GetStartTime(this Build build) => DevOpsUtil.ConvertFromRestTime(build.StartTime);

        public static DateTimeOffset? GetQueueTime(this Build build) => DevOpsUtil.ConvertFromRestTime(build.QueueTime);

        public static DateTimeOffset? GetFinishTime(this Build build) => DevOpsUtil.ConvertFromRestTime(build.FinishTime);

        public static int? GetByteSize(this BuildArtifact buildArtifact) => DevOpsUtil.GetArtifactByteSize(buildArtifact);

        public static BuildArtifactKind GetKind(this BuildArtifact buildArtifact) => DevOpsUtil.GetArtifactKind(buildArtifact);

        public static int GetAttempt(this Timeline timeline) => timeline.Records.Max(x => x.Attempt);

        public static bool IsAnySuccess(this TimelineRecord record) =>
            record.Result == TaskResult.Succeeded ||
            record.Result == TaskResult.SucceededWithIssues;

        public static bool IsAnyFailed(this TimelineRecord record) =>
            record.Result == TaskResult.Failed ||
            record.Result == TaskResult.Abandoned ||
            record.Result == TaskResult.Canceled;

        public static DateTimeOffset? GetStartTime(this TimelineRecord record) => DevOpsUtil.ConvertFromRestTime(record.StartTime);

        public static DateTimeOffset? GetFinishTime(this TimelineRecord record) => DevOpsUtil.ConvertFromRestTime(record.FinishTime);

        public static async Task<string?> GetJsonAsync(this HttpClient httpClient, string uri, Action<Exception>? onError = null)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, uri);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            try
            {
                var response = await httpClient.SendAsync(message).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                return null;
            }
        }

        public static async Task<string?> DownloadFileTextAsync(this HttpClient httpClient, string uri, Action<Exception>? onError = null)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, uri);
            try
            {
                var response = await httpClient.SendAsync(message).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                return null;
            }
        }

        public static async Task<MemoryStream?> DownloadFileStreamAsync(this HttpClient httpClient, string uri, Action<Exception>? onError = null)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, uri);
            try
            {
                var response = await httpClient.SendAsync(message).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var stream = new MemoryStream();
                await response.Content.CopyToAsync(stream).ConfigureAwait(false);
                stream.Position = 0;
                return stream;
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                return null;
            }
        }

        /// <summary>
        /// Get the timeline from the specified attempt
        /// </summary>
        public static async Task<Timeline?> GetTimelineAttemptAsync(this DevOpsServer server, string project, int buildNumber, int attempt)
        {
            var timeline = await server.GetTimelineAsync(project, buildNumber).ConfigureAwait(false);
            if (timeline is null)
            {
                return null;
            }

            if (timeline.Records.All(x => x.Attempt == attempt))
            {
                return timeline;
            }

            var attemptTimelineId = timeline
                .Records
                .Select(x => x.PreviousAttempts?.FirstOrDefault(x => x.Attempt == attempt))
                .Select(x => x?.TimelineId)
                .SelectNotNull()
                .FirstOrDefault();
            return await server.GetTimelineAsync(project, buildNumber, attemptTimelineId).ConfigureAwait(false);
        }

        public static Task<Timeline?> GetTimelineAttemptAsync(this DevOpsServer server, string project, int buildNumber, int? attempt) =>
            attempt is int attemptId
            ? GetTimelineAttemptAsync(server, project, buildNumber, attemptId)
            : server.GetTimelineAsync(project, buildNumber);

        /// <summary>
        /// List the time line attempts of the given build
        /// </summary>
        public static async Task<List<Timeline>> ListTimelineAttemptsAsync(this DevOpsServer server, string project, int buildNumber)
        {
            var list = new List<Timeline>();

            var timeline = await server.GetTimelineAsync(project, buildNumber).ConfigureAwait(false);
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
                var attemptTimeline = await server.GetTimelineAttemptAsync(project, buildNumber, attempt).ConfigureAwait(false);
                if (attemptTimeline is object)
                {
                    list.Add(attemptTimeline);
                }
            }

            return list;
        }

        public static Task<string> GetYamlAsync(this DevOpsServer server, string project, int buildNumber) =>
            server.GetBuildLogAsync(project, buildNumber, logId: 1);

        /// <summary>
        /// List the builds for the given pull request
        /// </summary>
        /// <remarks>
        /// The request can filter on the definitions that it built against
        /// 
        /// If the repositoryId REST argument is provided it must be accompanied by repositoryType
        /// </remarks>
        public static Task<List<Build>> ListPullRequestBuilds(
            this DevOpsServer server,
            in GitHubPullRequestKey prKey,
            string project,
            int[]? definitions = null)
        {
            var branchName = $"refs/pull/{prKey.Number}/merge";
            var repositoryInfo = prKey.GitHubInfo.RepositoryInfo;
            return server.ListBuildsAsync(
                project,
                definitions: definitions,
                branchName: branchName,
                repositoryId: repositoryInfo.Id,
                repositoryType: repositoryInfo.Type);
        }
    }
}