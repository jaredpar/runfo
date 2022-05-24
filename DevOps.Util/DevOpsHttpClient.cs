using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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

        private async Task DownloadWithProgress(HttpResponseMessage response, Stream destinationStream, TextWriter textWriter)
        {
            string output = "Downloading...";
            textWriter.Write(output);
            int lastLineLength = output.Length;
            using Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            long totalRead = 0L;
            long totalReads = 0L;
            byte[] buffer = new byte[8192];
            const double mbdividend = 1000000.0;
            double sizeInMbs = (response.Content.Headers.ContentLength ?? 0) / mbdividend;

            while (true)
            {
                int read = await contentStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                await destinationStream.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                totalRead += read;
                totalReads += 1;

                if (sizeInMbs != 0 && totalReads % 500 == 0)
                {
                    output = $"\rDownloading... {totalRead / mbdividend:0,0.00}/{sizeInMbs:0,0.00}MBs";
                    lastLineLength = output.Length;
                    textWriter.Write(output);
                }
            }
            textWriter.Write($"\r{new string(' ', lastLineLength)}\b\r"); // clear status line in text writer.
        }

        internal async Task DownloadFileAsync(string uri, Stream destinationStream, bool showProgress = false, TextWriter? writer = null, string? mimeType = null, bool resume = false)
        {
            var message = CreateHttpRequestMessage(HttpMethod.Get, uri);

            if (mimeType != null)
            {
                message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mimeType));
            }

            if (resume)
            {
                long startAt = destinationStream.Length;
                destinationStream.Position = startAt;
                message.Headers.Range = new RangeHeaderValue(startAt, null);
            }

            using var response = await HttpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            if (resume && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                var responseContentRange = response.Content.Headers.ContentRange;
                if (responseContentRange != null && responseContentRange.HasLength && responseContentRange.Length == destinationStream.Length)
                {
                    // if the file is already complete treat as success
                    return;
                }
            }

            response.EnsureSuccessStatusCode();

            if (!showProgress)
            {
                await response.Content.CopyToAsync(destinationStream).ConfigureAwait(false);
            }
            else
            {
                if (writer == null)
                    throw new ArgumentNullException(nameof(writer), $"Should not be null when {nameof(showProgress)} is true.");

                await DownloadWithProgress(response, destinationStream, writer).ConfigureAwait(false);
            }

        }

        internal Task DownloadFileAsync(string uri, string destinationFilePath, bool showProgress = false, TextWriter? writer = null, bool resume = false) =>
            WithFileStream(destinationFilePath, resume, fileStream => DownloadFileAsync(uri, fileStream, showProgress, writer));

        internal Task DownloadZipFileAsync(string uri, Stream destinationStream, bool showProgress = false, TextWriter? writer = null, bool resume = false) =>
            DownloadFileAsync(uri, destinationStream, showProgress, writer, mimeType: "application/zip", resume);

        internal Task DownloadZipFileAsync(string uri, string destinationFilePath, bool showProgress = false, TextWriter? writer = null, bool resume = false) =>
            WithFileStream(destinationFilePath, resume, fileStream => DownloadZipFileAsync(uri, fileStream, showProgress, writer, resume));

        internal async Task WithFileStream(string destinationFilePath, bool resume, Func<FileStream, Task> func)
        {
            using var fileStream = new FileStream(destinationFilePath, resume ? FileMode.OpenOrCreate : FileMode.Create,FileAccess.Write);
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
