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
        public string TimelineCacheDirectory { get; }
        public string TestRunsCacheDirectory { get; }
        public string TestResultsCacheDirectory { get; }

        public LocalAzureStorageUtil(string organization, string cacheDirectory)
        {
            Organization = organization;
            CacheDirectory = cacheDirectory;
            TimelineCacheDirectory = Path.Combine(cacheDirectory, "timelines");
            TestRunsCacheDirectory = Path.Combine(cacheDirectory, "testruns");
            TestResultsCacheDirectory = Path.Combine(cacheDirectory, "testresults");
        }

        private string GetFileName(string project, int buildNumber) => $"{Organization}-{project}-{buildNumber}.json";

        private string GetFileName(string project, int testRunId, TestOutcome[]? outcomes)
        {
            var o = "none";
            if (outcomes is object)
            {
                o = string.Join('-', outcomes.Select(x => x.ToString()));
            }

            return $"{Organization}-{project}-{testRunId}-{o}.json";
        }

        private static void SaveJson<T>(string directory, string fileName, List<T> value)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var filePath = Path.Combine(directory, fileName);

                using var fileStream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var streamWriter = new StreamWriter(fileStream);
                var jsonSerializer = new JsonSerializer();
                jsonSerializer.Serialize(streamWriter, value.ToArray());
            }
            catch
            {
                // Don't worry about cache errors
            }
        }

        private static List<T> LoadJson<T>(string directory, string fileName)
        {
            var filePath = Path.Combine(directory, fileName);

            using var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var streamReader = new StreamReader(fileStream);
            using var jsonTextReader = new JsonTextReader(streamReader);
            var jsonSerializer = new JsonSerializer();
            var array = jsonSerializer.Deserialize<T[]>(jsonTextReader);
            return new List<T>(array);
        }

        private List<Timeline> GetTimelineList(string project, int buildNumber) =>
            LoadJson<Timeline>(
                TimelineCacheDirectory,
                GetFileName(project, buildNumber));

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

        public Task SaveTimelineAsync(string project, int buildNumber, List<Timeline> timelineList, CancellationToken cancellationToken = default)
        {
            SaveJson(TimelineCacheDirectory, GetFileName(project, buildNumber), timelineList);
            return Task.CompletedTask;
        }

        public Task<List<TestRun>> ListTestRunsAsync(string project, int buildNumber, CancellationToken cancellationToken = default)
        {
            var list = LoadJson<TestRun>(TestRunsCacheDirectory, GetFileName(project, buildNumber));
            return Task.FromResult(list);
        }

        public Task SaveTestRunsAsync(string project, int buildNumber, List<TestRun> testRunList, CancellationToken cancellationToken = default)
        {
            SaveJson(TestRunsCacheDirectory, GetFileName(project, buildNumber), testRunList);
            return Task.CompletedTask;
        }

        public Task<List<TestCaseResult>> ListTestResultsAsync(string project, int testRunId, TestOutcome[]? outcomes = null, CancellationToken cancellationToken = default)
        {
            var list = LoadJson<TestCaseResult>(TestResultsCacheDirectory, GetFileName(project, testRunId, outcomes));
            return Task.FromResult(list);
        }

        public Task SaveTestResultsAsync(string project, int testRunId, TestOutcome[]? outcomes, List<TestCaseResult> testResults, CancellationToken cancellationToken = default)
        {
            SaveJson(TestResultsCacheDirectory, GetFileName(project, testRunId, outcomes), testResults);
            return Task.CompletedTask;
        }

    }
}