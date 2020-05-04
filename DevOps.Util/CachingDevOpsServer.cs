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
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace DevOps.Util
{
    public sealed class CachingDevOpsServer : DevOpsServer
    {
        internal string CacheDirectory { get; }

        public CachingDevOpsServer(string cacheDirectory, string organization, string personalAccessToken)
            : base(organization, personalAccessToken)
        {
            CacheDirectory = cacheDirectory;
        }

        public override async Task<string> GetJsonResult(string uri, bool cacheable = false)
        {
            if (!cacheable)
            {
                return await base.GetJsonResultCore(uri);
            }

            var key = GetKey(uri);
            using var fileStream = TryOpenCacheFile(key);
            if (fileStream is object)
            {
                using var reader = new StreamReader(fileStream, Encoding.UTF8);
                return await reader.ReadToEndAsync();
            }
            else
            {
                var response = await base.GetJsonResultCore(uri);
                await SaveCacheFile(key, Encoding.UTF8.GetBytes(response));
                return response;
            }
        }

        private FileStream? TryOpenCacheFile(string key)
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


        private async Task SaveCacheFile(string key, byte[] bytes)
        {
            try
            {
                Directory.CreateDirectory(CacheDirectory);
                var filePath = Path.Combine(CacheDirectory, key);
                using var cacheStream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await cacheStream.WriteAsync(bytes, 0, bytes.Length, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Don't worry about cache errors
            }
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