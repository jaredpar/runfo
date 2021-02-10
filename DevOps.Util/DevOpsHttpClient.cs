using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util
{
    public enum AuthorizationKind
    {
        None,
        PersonalAccessToken,
        BearerToken
    }

    public readonly struct AuthorizationToken
    {
        public static AuthorizationToken None => default;

        public AuthorizationKind AuthorizationKind { get; }
        public string Token { get; }

        public bool IsNone => AuthorizationKind == AuthorizationKind.None;

        public AuthorizationToken(AuthorizationKind authorizationKind, string token)
        {
            if (authorizationKind == AuthorizationKind.None)
            {
                throw new ArgumentException("", nameof(authorizationKind));
            }

            AuthorizationKind = authorizationKind;
            Token = token;
        }

        public override string ToString() => $"{AuthorizationKind} {Token}";
    }

    internal class DevOpsHttpClient
    {
        internal AuthorizationToken AuthorizationToken { get; }
        internal HttpClient HttpClient { get; }

        internal DevOpsHttpClient(AuthorizationToken authorizationToken = default, HttpClient? httpClient = null)
        {
            AuthorizationToken = authorizationToken;
            HttpClient = httpClient ?? new HttpClient();
        }

        internal async Task DownloadFileAsync(string uri, Stream destinationStream)
        {
            var message = CreateHttpRequestMessage(HttpMethod.Get, uri);
            using var response = await HttpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await response.Content.CopyToAsync(destinationStream).ConfigureAwait(false);
        }

        internal async Task DownloadZipFileAsync(string uri, Stream destinationStream)
        {
            var message = CreateHttpRequestMessage(HttpMethod.Get, uri);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/zip"));
            using var response = await HttpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await response.Content.CopyToAsync(destinationStream).ConfigureAwait(false);
        }

        internal Task DownloadZipFileAsync(string uri, string destinationFilePath) =>
            WithFileStream(destinationFilePath, fileStream => DownloadZipFileAsync(uri, fileStream));

        internal async Task WithFileStream(string destinationFilePath, Func<FileStream, Task> func)
        {
            using var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write);
            await func(fileStream).ConfigureAwait(false);
        }

        internal Task<MemoryStream> DownloadFileAsync(string uri) =>
            WithMemoryStream(s => DownloadFileAsync(uri, s));

        internal Task<MemoryStream> DownloadZipFileAsync(string uri) =>
            WithMemoryStream(s => DownloadFileAsync(uri, s));

        internal async Task<MemoryStream> WithMemoryStream(Func<MemoryStream, Task> func)
        {
            var stream = new MemoryStream();
            await func(stream).ConfigureAwait(false);
            stream.Position = 0;
            return stream;
        }

        internal Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri)
        {
            var request = CreateHttpRequestMessage(method, uri);
            return HttpClient.SendAsync(request);
        }

        internal Task<HttpResponseMessage> SendAsync(HttpRequestMessage message) => HttpClient.SendAsync(message);

        internal HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, string uri)
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
