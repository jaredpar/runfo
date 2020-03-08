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
using System.Security.Cryptography;

namespace DevOps.Util
{
    public sealed class CachingDevOpsServer : DevOpsServer
    {
        internal string CacheDirectory { get; }

        public CachingDevOpsServer(string organization, string personalAccessToken)
            : base(organization, personalAccessToken)
        {
            CacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "runfo");
        }

        protected override Task DownloadZipFileCoreAsync(string uri, Stream destinationStream) =>
            CacheFileDownloadAsync(uri, destinationStream, base.DownloadZipFileCoreAsync);

        protected override Task DownloadFileCoreAsync(string uri, Stream destinationStream) =>
            CacheFileDownloadAsync(uri, destinationStream, base.DownloadFileCoreAsync);

        private async Task CacheFileDownloadAsync(string uri, Stream destinationStream, Func<string, Stream, Task> downloadFunc)
        {
            var key = GetKey(uri);
            lock (this)
            {
                File.AppendAllLines(Path.Combine(CacheDirectory, "list.txt"), new[] { $"{key} {uri}" });
            }

            var fileStream = TryOpenCacheFile(key);
            if (fileStream is object)
            {
                await fileStream.CopyToAsync(destinationStream).ConfigureAwait(false);
                return;
            }

            using var cacheStream = new MemoryStream();
            await downloadFunc(uri, cacheStream).ConfigureAwait(false);
            await SaveCacheFile(key, cacheStream);
            await cacheStream.CopyToAsync(destinationStream).ConfigureAwait(false);
        }

        private FileStream TryOpenCacheFile(string key)
        {
            var filePath = Path.Combine(CacheDirectory, key);
            try
            {
                Directory.CreateDirectory(CacheDirectory);
                if (File.Exists(filePath))
                {
                    return File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
            }
            catch
            {
                // Don't worry about cache errors here.
            }

            return null;
        }


        private async Task SaveCacheFile(string key, MemoryStream stream)
        {
            stream.Position = 0;
            try
            {
                Directory.CreateDirectory(CacheDirectory);
                var filePath = Path.Combine(CacheDirectory, key);
                using var cacheStream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(cacheStream).ConfigureAwait(false);
            }
            catch
            {
                // Don't worry about cache errors
            }

            stream.Position = 0;
        }

        private string GetKey(string uri)
        {
            var encoding = Encoding.UTF8;
            var bytes = encoding.GetBytes(uri);
            var hashedBytes = SHA256.Create().ComputeHash(bytes);
            return string.Concat(hashedBytes.Select(x => x.ToString("x2")));
        }
    }
}