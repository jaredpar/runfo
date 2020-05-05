#nullable enable

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
    public interface IAzureClient
    {
        HttpClient HttpClient { get; }

        bool IsAuthenticated { get; }

        Task<string> GetTextAsync(string uri);

        Task<string> GetJsonAsync(string uri, bool cacheable = false);

        Task<string> GetJsonWithRetryAsync(string uri, bool cacheable, Func<HttpResponseMessage, Task<bool>> predicate);

        Task<(string Json, string? ContinuationToken)> GetJsonAndContinuationTokenAsync(string uri);

        Task DownloadFileAsync(string uri, Stream destinationStream);

        Task DownloadZipFileAsync(string uri, Stream destinationStream);
    }

    internal static class AzureJsonUtil
    {
        internal static T[] GetArray<T>(string json)
        {
            var root = JObject.Parse(json);
            var array = (JArray)root["value"];
            return array.ToObject<T[]>();
        }

        internal static T GetObject<T>(string json) => JsonConvert.DeserializeObject<T>(json);
    }

    public class AzureClient : IAzureClient
    { 
        public HttpClient HttpClient { get; }

        private string? PersonalAccessToken { get; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(PersonalAccessToken);

        public AzureClient(string? personalAccessToken = null)
            : this(new HttpClient(), personalAccessToken)
        {
        }

        public AzureClient(HttpClient httpClient, string? personalAccessToken = null)
        {
            HttpClient = httpClient;
            PersonalAccessToken = personalAccessToken;
        }

        private HttpRequestMessage CreateHttpRequestMessage(string uri, HttpMethod? method = null)
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

        public async Task<string> GetTextAsync(string uri)
        {
            var message = CreateHttpRequestMessage(uri);
            using var response = await HttpClient.SendAsync(message).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return responseBody;
        }

        public virtual async Task<string> GetJsonAsync(string uri, bool cacheable)
        {
            var message = CreateHttpRequestMessage(uri);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await HttpClient.SendAsync(message).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return responseBody;
        }

        public async Task<string> GetJsonWithRetryAsync(string uri, bool cacheable, Func<HttpResponseMessage, Task<bool>> predicate)
        {
            do
            {
                var message = CreateHttpRequestMessage(uri);
                message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await HttpClient.SendAsync(message).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var tryAgain = await predicate(response).ConfigureAwait(false);
                    if (tryAgain)
                    {
                        continue;
                    }
                }
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return responseBody;

            } while (true);
        }

        public async Task<(string Json, string? ContinuationToken)> GetJsonAndContinuationTokenAsync(string uri)
        {
            var message = CreateHttpRequestMessage(uri);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await HttpClient.SendAsync(message).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string? continuationToken = null;
            if (response.Headers.TryGetValues("x-ms-continuationtoken", out var values))
            {
                continuationToken = values.FirstOrDefault();
            }

            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return (responseBody, continuationToken);
        }

        public async Task DownloadZipFileAsync(string uri, Stream destinationStream)
        {
            var message = CreateHttpRequestMessage(uri);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/zip"));
            using var response = await HttpClient.SendAsync(message).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await response.Content.CopyToAsync(destinationStream).ConfigureAwait(false);
        }

        public async Task DownloadFileAsync(string uri, Stream destinationStream)
        {
            var message = CreateHttpRequestMessage(uri);
            using var response = await HttpClient.SendAsync(message).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await response.Content.CopyToAsync(destinationStream).ConfigureAwait(false);
        }
    }
}