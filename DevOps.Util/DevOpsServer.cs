using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util
{
    public class DevOpsServer
    {
        private HttpClient HttpClient { get;}

        private string PersonalAccessToken { get; }
        public string Organization { get; }

        public DevOpsServer(string organization, string personalAccessToken = null)
        {
            Organization = organization;
            PersonalAccessToken = personalAccessToken;
            HttpClient = new HttpClient();
        }

        /// <summary>
        /// List the builds that meet the provided query parameters
        /// </summary>
        /// <param name="buildNumber">Supports int based build numbers or * prefixes</param>
        public async Task<List<Build>> ListBuildsAsync(
            string project,
            IEnumerable<int> definitions = null,
            IEnumerable<int> queues = null,
            string buildNumber = null,
            DateTimeOffset? minTime = null,
            DateTimeOffset? maxTime = null,
            string requestedFor = null,
            BuildReason? reasonFilter = null,
            BuildStatus? statusFilter = null,
            BuildResult? resultFilter = null,
            int? top = null,
            int? maxBuildsPerDefinition = null,
            QueryDeletedOption? deletedFilter = null,
            BuildQueryOrder? queryOrder = null,
            string branchName = null,
            IEnumerable<int> buildIds = null,
            string repositoryId = null,
            string repositoryType = null)
        {
            var builder = GetBuilder(project, "build/builds");

            builder.AppendList("definitions", definitions);
            builder.AppendList("queues", queues);
            builder.AppendString("buildNumber", buildNumber);
            builder.AppendDateTime("minTime", minTime);
            builder.AppendDateTime("maxTime", maxTime);
            builder.AppendString("requestedFor", requestedFor);
            builder.AppendEnum("reasonFilter", reasonFilter);
            builder.AppendEnum("statusFilter", statusFilter);
            builder.AppendEnum("resultFilter", resultFilter);
            builder.AppendInt("$top", top);
            builder.AppendInt("maxBuildsPerDefinition", maxBuildsPerDefinition);
            builder.AppendEnum("deletedFilter", deletedFilter);
            builder.AppendEnum("queryOrder", queryOrder);
            builder.AppendString("branchName", branchName);
            builder.AppendList("buildIds", buildIds);
            builder.AppendString("repositoryId", repositoryId);
            builder.AppendString("repositoryType", repositoryType);
            return await ListItemsCore<Build>(builder, limit: top).ConfigureAwait(false);
        }

        /// <summary>
        /// List the builds that meet the provided query parameters
        /// </summary>
        public IAsyncEnumerable<Build> EnumerateBuildsAsync(
            string project,
            IEnumerable<int> definitions = null,
            IEnumerable<int> queues = null,
            string buildNumber = null,
            DateTimeOffset? minTime = null,
            DateTimeOffset? maxTime = null,
            string requestedFor = null,
            BuildReason? reasonFilter = null,
            BuildStatus? statusFilter = null,
            BuildResult? resultFilter = null,
            int? top = null,
            int? maxBuildsPerDefinition = null,
            QueryDeletedOption? deletedFilter = null,
            BuildQueryOrder? queryOrder = null,
            string branchName = null,
            IEnumerable<int> buildIds = null,
            string repositoryId = null,
            string repositoryType = null)
        {
            var builder = GetBuilder(project, "build/builds");

            builder.AppendList("definitions", definitions);
            builder.AppendList("queues", queues);
            builder.AppendString("buildNumber", buildNumber);
            builder.AppendDateTime("minTime", minTime);
            builder.AppendDateTime("maxTime", maxTime);
            builder.AppendString("requestedFor", requestedFor);
            builder.AppendEnum("reasonFilter", reasonFilter);
            builder.AppendEnum("statusFilter", statusFilter);
            builder.AppendEnum("resultFilter", resultFilter);
            builder.AppendInt("$top", top);
            builder.AppendInt("maxBuildsPerDefinition", maxBuildsPerDefinition);
            builder.AppendEnum("deletedFilter", deletedFilter);
            builder.AppendEnum("queryOrder", queryOrder);
            builder.AppendString("branchName", branchName);
            builder.AppendList("buildIds", buildIds);
            builder.AppendString("repositoryId", repositoryId);
            builder.AppendString("repositoryType", repositoryType);
            return EnumerateItemsAsync<Build>(builder, limit: top);
        }

        public Task<Build> GetBuildAsync(string project, int buildId)
        {
            var builder = GetBuilder(project, $"build/builds/{buildId}");
            return GetJsonResult<Build>(builder);
        }

        public Task<BuildLog[]> GetBuildLogsAsync(string project, int buildId)
        {
            var builder = GetBuilder(project, $"build/builds/{buildId}/logs");
            return GetJsonArrayResult<BuildLog>(builder);
        }

        public Task DownloadBuildLogsAsync(string project, int buildId, Stream stream)
        {
            var builder = GetBuilder(project, $"build/builds/{buildId}/logs");
            return DownloadZipFileAsync(builder.ToString(), stream);
        }

        public Task<MemoryStream> DownloadBuildLogsAsync(string project, int buildId) =>
            WithMemoryStream(s => DownloadBuildLogsAsync(project, buildId));

        private RequestBuilder GetBuildLogRequestBuilder(string project, int buildId, int logId) =>
            GetBuilder(project, $"build/builds/{buildId}/logs/{logId}");

        public async Task<string> GetBuildLogAsync(string project, int buildId, int logId, int? startLine = null, int? endLine = null)
        {
            var builder = GetBuildLogRequestBuilder(project, buildId, logId);
            builder.AppendInt("startLine", startLine);
            builder.AppendInt("endLine", endLine);

            using (var response = await GetAsync(builder.ToString()).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return responseBody;
            }
        }

        public Task DownloadBuildLogAsync(string project, int buildId, int logId, Stream destinationStream) =>
            DownloadFileAsync(
                GetBuildLogRequestBuilder(project, buildId, logId).ToString(),
                destinationStream);

        public Task DownloadBuildLogAsync(string project, int buildId, int logId, string destinationFilePath) =>
            WithFileStream(
                destinationFilePath,
                stream => DownloadBuildLogAsync(project, buildId, logId, stream));

        public Task<MemoryStream> DownloadBuildLogAsync(string project, int buildId, int logId) =>
            WithMemoryStream(stream => DownloadBuildLogAsync(project, buildId, logId, stream));

        public Task<Timeline> GetTimelineAsync(string project, int buildId)
        {
            var builder = GetBuilder(project, $"build/builds/{buildId}/timeline");
            return GetJsonResult<Timeline>(builder);
        }

        public Task<Timeline> GetTimelineAsync(Build build) => GetTimelineAsync(build.Project.Name, build.Id);

        public Task<Timeline> GetTimelineAsync(string project, int buildId, string timelineId, int? changeId = null)
        {
            var builder = GetBuilder(project, $"build/builds/{buildId}/timeline/{timelineId}");
            builder.AppendInt("changeId", changeId);
            return GetJsonResult<Timeline>(builder);
        }

        public Task<List<BuildArtifact>> ListArtifactsAsync(string project, int buildId)
        {
            var builder = GetBuilder(project, $"build/builds/{buildId}/artifacts");
            return ListItemsCore<BuildArtifact>(builder);
        }

        public async Task<List<BuildArtifact>> ListArtifactsAsync(Build build) => await ListArtifactsAsync(build.Project.Name, build.Id);

        private string GetArtifactUri(string project, int buildId, string artifactName)
        {
            var builder = GetBuilder(project, $"build/builds/{buildId}/artifacts");
            builder.AppendString("artifactName", artifactName);
            return builder.ToString();
        }

        public async Task<BuildArtifact> GetArtifactAsync(string project, int buildId, string artifactName)
        {
            var uri = GetArtifactUri(project, buildId, artifactName);
            var json = await GetJsonResult(uri).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<BuildArtifact>(json);
        }

        public Task<MemoryStream> DownloadArtifactAsync(string project, int buildId, string artifactName) =>
            WithMemoryStream(s => DownloadArtifactAsync(project, buildId, artifactName, s));

        public Task DownloadArtifactAsync(string project, int buildId, string artifactName, Stream stream)
        {
            var uri = GetArtifactUri(project, buildId, artifactName);
            return DownloadZipFileAsync(uri, stream);
        }

        public Task<List<TeamProjectReference>> ListProjectsAsync(ProjectState? stateFilter = null, int? top = null, int? skip = null, bool? getDefaultTeamImageUrl = null)
        {
            var builder = GetBuilder(project: null, apiPath: "projects");
            builder.AppendEnum("stateFilter", stateFilter);
            builder.AppendInt("$top", top);
            builder.AppendInt("$skip", skip);
            builder.AppendBool("getDefaultTeamImageUrl", getDefaultTeamImageUrl);
            return ListItemsCore<TeamProjectReference>(builder, limit: top);
        }

        public Task<List<DefinitionReference>> ListDefinitionsAsync(
            string project,
            IEnumerable<int> definitions = null,
            int? top = null)
        {
            var builder = GetBuilder(project, "build/definitions");
            builder.AppendList("definitionIds", definitions);
            builder.AppendInt("$top", top);
            return ListItemsCore<DefinitionReference>(builder, limit: top);
        }

        public Task<BuildDefinition> GetDefinitionAsync(
            string project,
            int definitionId,
            int? revision = null)
        {
            var builder = GetBuilder(project, $"build/definitions/{definitionId}");
            builder.AppendInt("revision", revision);
            return GetJsonResult<BuildDefinition>(builder);
        }

        public Task<TestRun[]> ListTestRunsAsync(
            string project,
            Uri buildUri,
            int? skip = null,
            int? top = null)
        {
            EnsurePersonalAuthorizationTokenForTests();
            var builder = GetBuilder(project, $"test/runs");
            builder.AppendUri("buildUri", buildUri);
            builder.AppendInt("$skip", top);
            builder.AppendInt("$top", top);
            return GetJsonArrayResult<TestRun>(builder);
        }

        public Task<TestRun[]> ListTestRunsAsync(
            string project,
            int buildId,
            int? skip = null,
            int? top = null)
        {
            var uri = $"vstfs:///Build/Build/{buildId}";
            return ListTestRunsAsync(
                project,
                new Uri(uri),
                skip: skip,
                top: top);
        }

        public Task<TestCaseResult[]> ListTestResultsAsync(
            string project,
            int runId,
            TestOutcome[] outcomes = null,
            int? skip = null,
            int? top = null)
        {
            EnsurePersonalAuthorizationTokenForTests();
            var builder = GetBuilder(project, $"test/runs/{runId}/results");
            builder.AppendList("outcomes", outcomes);
            builder.AppendInt("$top", top);
            builder.AppendInt("$skip", skip);
            return GetJsonArrayResult<TestCaseResult>(builder);
        }

        public Task<TestAttachment[]> GetTestCaseResultAttachmentsAsync(
            string project,
            int runId,
            int testCaseResultId)
        {
            EnsurePersonalAuthorizationTokenForTests();
            var builder = GetBuilder(project, $"test/Runs/{runId}/Results/{testCaseResultId}/attachments");
            builder.ApiVersion = "5.1-preview.1";
            return GetJsonArrayResult<TestAttachment>(builder);
        }

        public Task DownloadTestCaseResultAttachmentZipAsync(
            string project,
            int runId,
            int testCaseResultId,
            int attachmentId,
            Stream destinationStream)
        {
            EnsurePersonalAuthorizationTokenForTests();
            var builder = GetBuilder(project, $"test/Runs/{runId}/Results/{testCaseResultId}/attachments/{attachmentId}");
            builder.ApiVersion = "5.1-preview.1";
            return DownloadZipFileAsync(builder.ToString(), destinationStream);
        }

        public Task<MemoryStream> DownloadTestCaseResultAttachmentZipAsync(
            string project,
            int runId,
            int testCaseResultId,
            int attachmentId) => WithMemoryStream(s => DownloadTestCaseResultAttachmentZipAsync(project, runId, testCaseResultId, attachmentId, s));

        public Task DownloadTestCaseResultAttachmentZipAsync(
            string project,
            int runId,
            int testCaseResultId,
            int attachmentId,
            string destinationFilePath) =>
            WithFileStream(destinationFilePath, s => DownloadTestCaseResultAttachmentZipAsync(project, runId, testCaseResultId, attachmentId, s));

        private RequestBuilder GetBuilder(string project, string apiPath) => new RequestBuilder(Organization, project, apiPath);

        private async Task<string> GetJsonResult(string url) => (await GetJsonResultAndContinuationToken(url).ConfigureAwait(false)).Body;

        private async Task<T> GetJsonResult<T>(RequestBuilder builder)
        {
            var json = await GetJsonResult(builder.ToString()).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(json);
        }

        private async Task<T[]> GetJsonArrayResult<T>(RequestBuilder builder)
        {
            var json = await GetJsonResult(builder.ToString()).ConfigureAwait(false);
            var root = JObject.Parse(json);
            var array = (JArray)root["value"];
            return array.ToObject<T[]>();
        }

        private async Task<(string Body, string ContinuationToken)> GetJsonResultAndContinuationToken(string url)
        {
            var message = CreateHttpRequestMessage(url);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await HttpClient.SendAsync(message).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string continuationToken = null;
            if (response.Headers.TryGetValues("x-ms-continuationtoken", out var values))
            {
                continuationToken = values.FirstOrDefault();
            }

            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return (responseBody, continuationToken);
        }

        public Task DownloadFileAsync(string uri, Stream destinationStream) =>
            DownloadFileCoreAsync(uri, destinationStream);

        protected virtual async Task DownloadFileCoreAsync(string uri, Stream destinationStream)
        {
            using (var response = await GetAsync(uri).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await response.Content.CopyToAsync(destinationStream).ConfigureAwait(false);
            }
        }

        public Task<MemoryStream> DownloadFileAsync(string uri) =>
            WithMemoryStream(s => DownloadFileAsync(uri, s));

        public Task DownloadZipFileAsync(string uri, Stream destinationStream) => 
            DownloadZipFileCoreAsync(uri, destinationStream);

        protected virtual async Task DownloadZipFileCoreAsync(string uri, Stream destinationStream)
        {
            var message = CreateHttpRequestMessage(uri);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/zip"));

            using (var response = await HttpClient.SendAsync(message).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await response.Content.CopyToAsync(destinationStream).ConfigureAwait(false);
            }
        }

        public Task DownloadZipFileAsync(string uri, string destinationFilePath) =>
            WithFileStream(destinationFilePath, fileStream => DownloadZipFileAsync(uri, fileStream));

        public Task<MemoryStream> DownloadZipFileAsync(string uri) =>
            WithMemoryStream(s => DownloadFileAsync(uri, s));

        private HttpRequestMessage CreateHttpRequestMessage(string uri, HttpMethod method = null)
        {
            var message = new HttpRequestMessage(method ?? HttpMethod.Get, uri);
            if (!string.IsNullOrEmpty(PersonalAccessToken))
            {
                message.Headers.Authorization =  new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes($":{PersonalAccessToken}")));
            }

            return message;
        }

        private Task<HttpResponseMessage> GetAsync(string uri)
        {
            var message = CreateHttpRequestMessage(uri, HttpMethod.Get);
            return HttpClient.SendAsync(message);
        }

        private async Task<MemoryStream> WithMemoryStream(Func<MemoryStream, Task> func)
        {
            var stream = new MemoryStream();
            await func(stream).ConfigureAwait(false);
            stream.Position = 0;
            return stream;
        }

        private async Task WithFileStream(string destinationFilePath, Func<FileStream, Task> func)
        {
            using var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write);
            await func(fileStream).ConfigureAwait(false);
        }

        /// <summary>
        /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0
        /// </summary>
        private async Task<List<T>> ListItemsCore<T>(
            RequestBuilder builder, 
            int? limit = null)
        {
            var list = new List<T>();
            await foreach (var item in EnumerateItemsAsync<T>(builder).ConfigureAwait(false))
            {
                list.Add(item);
            }

            return list;
        }

        /// <summary>
        /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0
        /// </summary>
        private async IAsyncEnumerable<T> EnumerateItemsAsync<T>(
            RequestBuilder builder, 
            int? limit = null)
        {
            Debug.Assert(string.IsNullOrEmpty(builder.ContinuationToken));
            var count = 0;
            do
            {
                var (json, token) = await GetJsonResultAndContinuationToken(builder.ToString()).ConfigureAwait(false);
                var root = JObject.Parse(json);
                var array = (JArray)root["value"];
                var items = array.ToObject<T[]>();
                foreach (var item in items)
                {
                    yield return item;
                }

                count += items.Length;
                if (token is null)
                {
                    break;
                }

                if (limit.HasValue && count > limit.Value)
                {
                    break;
                }

                builder.ContinuationToken = token;
            } while (true);
        }

        private void EnsurePersonalAuthorizationTokenForTests()
        {
            if (string.IsNullOrEmpty(PersonalAccessToken))
            {
                throw new InvalidOperationException("Must have a personal access token specified to view test information");
            }
        }
    }
}
