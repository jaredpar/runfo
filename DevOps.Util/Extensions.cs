#nullable enable

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace DevOps.Util
{
    public static class DevOpsUtilExtensions
    {
        public static BuildKey GetBuildKey(this Build build) => DevOpsUtil.GetBuildKey(build);

        public static BuildInfo GetBuildInfo(this Build build) => DevOpsUtil.GetBuildInfo(build);

        public static BuildDefinitionInfo GetBuildDefinitionInfo(this Build build) => DevOpsUtil.GetBuildDefinitionInfo(build);

        public static DateTimeOffset? GetStartTime(this Build build) => DevOpsUtil.ConvertFromRestTime(build.StartTime);

        public static DateTimeOffset? GetQueueTime(this Build build) => DevOpsUtil.ConvertFromRestTime(build.QueueTime);

        public static DateTimeOffset? GetFinishTime(this Build build) => DevOpsUtil.ConvertFromRestTime(build.FinishTime);

        public static int? GetByteSize(this BuildArtifact buildArtifact) => DevOpsUtil.GetArtifactByteSize(buildArtifact);

        public static BuildArtifactKind GetKind(this BuildArtifact buildArtifact) => DevOpsUtil.GetArtifactKind(buildArtifact);

        public static bool IsAnySuccess(this TimelineRecord record) =>
            record.Result == TaskResult.Succeeded ||
            record.Result == TaskResult.SucceededWithIssues;

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
    }
}