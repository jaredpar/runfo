using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util
{
    public class BaseServer
    {
        public AuthorizationToken AuthorizationToken { get; }
        public HttpClient HttpClient { get; }

        public BaseServer(AuthorizationToken authorizationToken = default, HttpClient? httpClient = null)
        {
            AuthorizationToken = authorizationToken;
            HttpClient = httpClient ?? new HttpClient();
        }

        public async Task DownloadFileAsync(string uri, Stream destinationStream)
        {
            var message = CreateHttpRequestMessage(HttpMethod.Get, uri);
            using var response = await HttpClient.SendAsync(message).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await response.Content.CopyToAsync(destinationStream).ConfigureAwait(false);
        }

        public async Task DownloadZipFileAsync(string uri, Stream destinationStream)
        {
            var message = CreateHttpRequestMessage(HttpMethod.Get, uri);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/zip"));
            using var response = await HttpClient.SendAsync(message).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await response.Content.CopyToAsync(destinationStream).ConfigureAwait(false);
        }

        public Task DownloadZipFileAsync(string uri, string destinationFilePath) =>
            WithFileStream(destinationFilePath, fileStream => DownloadZipFileAsync(uri, fileStream));

        protected async Task WithFileStream(string destinationFilePath, Func<FileStream, Task> func)
        {
            using var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write);
            await func(fileStream).ConfigureAwait(false);
        }

        public Task<MemoryStream> DownloadFileAsync(string uri) =>
            WithMemoryStream(s => DownloadFileAsync(uri, s));

        public Task<MemoryStream> DownloadZipFileAsync(string uri) =>
            WithMemoryStream(s => DownloadFileAsync(uri, s));

        protected async Task<MemoryStream> WithMemoryStream(Func<MemoryStream, Task> func)
        {
            var stream = new MemoryStream();
            await func(stream).ConfigureAwait(false);
            stream.Position = 0;
            return stream;
        }

        protected HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, string uri)
        {
            var message = new HttpRequestMessage(method ?? HttpMethod.Get, uri);
            switch (AuthorizationToken.AuthorizationKind)
            {
                case AuthorizationKind.PersonalAccessToken:
                    message.Headers.Authorization = new AuthenticationHeaderValue(
                        "Basic",
                        Convert.ToBase64String(Encoding.ASCII.GetBytes($":{AuthorizationToken.Token}")));
                    break;
                case AuthorizationKind.BearerToken:
                    message.Headers.Authorization = new AuthenticationHeaderValue(
                        "Bearer",
                        AuthorizationToken.Token);
                    break;
            }

            return message;
        }
    }
}
