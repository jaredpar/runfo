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
using DevOps.Util.DotNet;

namespace DevOps.Util.DotNet
{
    public sealed class LocalAzureStorageUtil : IAzureStorageUtil
    {
        public string Organization { get; }
        public string CacheDirectory { get; }

        public LocalAzureStorageUtil(string organization, string cacheDirectory)
        {
            Organization = organization;
            CacheDirectory = cacheDirectory;
        }

        private string GetFileName(string project, int buildNumber) => $"{Organization}-{project}-{buildNumber}.json";

        public Task SaveTimelineAsync(string project, int buildNumber, List<Timeline> timelineList, CancellationToken cancellationToken = default)
        {
            try
            {
                Directory.CreateDirectory(CacheDirectory);
                var filePath = Path.Combine(CacheDirectory, GetFileName(project, buildNumber));

                using var fileStream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var streamWriter = new StreamWriter(fileStream);
                var jsonSerializer = new JsonSerializer();
                jsonSerializer.Serialize(streamWriter, timelineList.ToArray());
            }
            catch
            {
                // Don't worry about cache errors
            }

            return Task.CompletedTask;
        }

        public List<Timeline> GetTimelineList(string project, int buildNumber)
        {
            var filePath = Path.Combine(CacheDirectory, GetFileName(project, buildNumber));

            using var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var streamReader = new StreamReader(fileStream);
            using var jsonTextReader = new JsonTextReader(streamReader);
            var jsonSerializer = new JsonSerializer();
            var array = jsonSerializer.Deserialize<Timeline[]>(jsonTextReader);

            return new List<Timeline>(array);
        }

        public Task<Timeline> GetTimelineAttemptAsync(string project, int buildNumber, int attempt, CancellationToken cancellationToken = default)
        {
            var timeline = GetTimelineList(project, buildNumber).First(x => x.GetAttempt() == attempt);
            return Task.FromResult(timeline);
        }

        public Task<Timeline> GetTimelineAsync(string project, int buildNumber, CancellationToken cancellationToken = default)
        {
            var timeline = GetTimelineList(project, buildNumber)
                .OrderByDescending(x => x.GetAttempt())
                .First();
            return Task.FromResult(timeline);
        }
    }
}