using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.IO;
using System.Text.RegularExpressions;

namespace DevOps.Util.DotNet
{
    /// <summary>
    /// Utility for logging clone times
    /// </summary>
    public sealed class CloneTimeUtil : IDisposable
    {
        public const string ProjectName = "public";
        public const int BuildDefinitionId = 15;

        public SqlConnection SqlConnection { get; }
        public DevOpsServer DevOpsServer { get; }
        public ILogger Logger { get; }

        public CloneTimeUtil(string sqlConnectionString, ILogger logger = null)
        {
            Logger = logger ?? DotNetUtil.CreateConsoleLogger();
            DevOpsServer = new DevOpsServer("dnceng");
            SqlConnection = new SqlConnection(sqlConnectionString);
        }

        public void Dispose()
        {
            SqlConnection.Dispose();
        }

        public async Task<List<Build>> ListBuildsAsync(int top) => await DevOpsServer.ListBuildsAsync(ProjectName, new[] { BuildDefinitionId }, top: top);

        public async Task UpdateDatabaseAsync(int? top = null)
        {
            var builds = await DevOpsServer.ListBuildsAsync(ProjectName, top: top);
            foreach (var build in builds)
            {
                try
                {
                    await UploadBuild(build);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Unable to upload {build.Id}: {ex.Message}");
                }
            }
        }

        public async Task<bool> IsBuildUploadedAsync(int buildId)
        {
            if (SqlConnection.State != ConnectionState.Open)
            {
                await SqlConnection.OpenAsync();
            }

            var query = "SELECT * FROM dbo.BuildCloneTime WHERE BuildId = @BuildId";
            using var command = new SqlCommand(query, SqlConnection);
            command.Parameters.AddWithValue("@BuildId", buildId);
            using var reader = await command.ExecuteReaderAsync();
            return reader.HasRows;
        }

        public async Task UploadBuildAsync(int buildId)
        {
            var build = await DevOpsServer.GetBuildAsync(ProjectName, buildId);
            await UploadBuild(build);
        }

        private async Task UploadBuild(Build build)
        {
            try
            {
                var uri = DevOpsUtil.GetBuildUri(build);

                if (build.Status != BuildStatus.Completed)
                {
                    Logger.LogInformation($"Build is not completed {uri}");
                    return;
                }

                if (await IsBuildUploadedAsync(build.Id))
                {
                    Logger.LogInformation($"Build already uploaded {uri}");
                    return;
                }

                Logger.LogInformation($"Getting timeline {uri}");
                var jobs = await GetJobCloneTimesAsync(build);
                if (jobs.Count == 0)
                {
                    Logger.LogInformation("Found no jobs");
                    return;
                }

                Logger.LogInformation($"Uploading {uri}");
                if (build.StartTime is null)
                {
                    Logger.LogError("Found no start time");
                    return;
                }

                var buildStartTime = DateTimeOffset.Parse(build.StartTime);
                await DotNetUtil.DoWithTransactionAsync(SqlConnection, $"Upload Clone {build.Id}", async transaction =>
                {
                    foreach (var job in jobs)
                    {
                        await UploadJobCloneTime(transaction, build.Id, build.Definition.Id, buildStartTime, uri, job);
                    }

                    var minDuration = jobs.Min(x => x.Duration);
                    var maxDuration = jobs.Max(x => x.Duration);
                    var totalFetchSize = jobs.Sum(x => x.FetchSize);
                    var minFetchSpeed = jobs.Min(x => x.MinFetchSpeed);
                    var maxFetchSpeed = jobs.Max(x => x.MaxFetchSpeed);
                    await UploadBuildCloneTime(transaction, build.Id, build.Definition.Id, minDuration, maxDuration, buildStartTime, uri, totalFetchSize, minFetchSpeed, maxFetchSpeed);
                });

                Logger.LogInformation("Build upload complete");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error {ex.Message}");
                throw;
            }
        }

        public async Task<List<JobCloneTime>> GetJobCloneTimesAsync(Build build)
        {
            var list = new List<JobCloneTime>();
            var timeline = await DevOpsServer.GetTimelineAsync(ProjectName, build.Id);
            if (timeline is null)
            {
                return list;
            }

            foreach (var record in timeline.Records.Where(x => x.Name == "Checkout" && x.FinishTime is object && x.StartTime is object))
            {
                var duration = DateTime.Parse(record.FinishTime) - DateTime.Parse(record.StartTime);
                var startTime = DateTimeOffset.Parse(record.StartTime);
                var parent = timeline.Records.Single(x => x.Id == record.ParentId);
                var (fetchSize, minFetchSpeed, maxFetchSpeed, averageFetchSpeed) = await GetSizesAsync(record);
                var jobCloneTime = new JobCloneTime(
                    parent.Name,
                    startTime,
                    duration,
                    fetchSize,
                    minFetchSpeed: minFetchSpeed,
                    maxFetchSpeed: maxFetchSpeed,
                    averageFetchSpeed: averageFetchSpeed);
                list.Add(jobCloneTime);
            }

            return list;
        }

        private async Task<(double? FetchSize, double? MinFetchSpeed, double? MaxFetchSpeed, double? AverageFetchSpeed)> GetSizesAsync(TimelineRecord record)
        {
            try
            {
                using var stream = await DevOpsServer.DownloadFileAsync(record.Log.Url);
                using var reader = new StreamReader(stream);
                var sizeAtom = @"[\d.]+\s+[GMK]iB";
                var regex = new Regex($@"Receiving objects:\s+\d+% .*,\s+({sizeAtom})\s+\|\s+({sizeAtom})/s(,\s*done)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                var fetchSpeeds = new List<double>();
                double? fetchSize = null;

                do
                {
                    var line = await reader.ReadLineAsync();
                    if (line is null)
                    {
                        break;
                    }

                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        var size= parseSize(match.Groups[1].Value);
                        var speed = parseSize(match.Groups[2].Value);
                        if (speed.HasValue)
                        {
                            fetchSpeeds.Add(speed.Value);
                        }

                        updateIfBigger(ref fetchSize, size);

                        if (!string.IsNullOrEmpty(match.Groups[3].Value))
                        {
                            break;
                        }
                    }
                } while (true);

                if (fetchSpeeds.Count == 0)
                {
                    return (fetchSize, null, null, null);
                }

                return (
                    fetchSize,
                    fetchSpeeds.Min(),
                    fetchSpeeds.Max(),
                    fetchSpeeds.Average());
            }
            catch (Exception ex)
            {
                Logger.LogInformation($"Error parsing fetch times: {ex.Message}");
                return (null, null, null, null);
            }

            static void updateIfBigger(ref double? storage, double? value)
            {
                if (value is null)
                {
                    return;
                }

                if (storage is null || storage.Value < value)
                {
                    storage = value;
                }
            }

            static double? parseSize(string size)
            {
                var elements = size.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var value = double.Parse(elements[0]);
                return elements[1] switch
                {
                    "KiB" => value,
                    "MiB" => value * 1024,
                    "GiB" => value * 1024 * 1024,
                    _ => (double?)null
                };
            }
        }

        private async Task UploadJobCloneTime(SqlTransaction transaction, int buildId, int definitionId, DateTimeOffset buildStartTime, Uri buildUri, JobCloneTime jobCloneTime)
        {
            var query = "INSERT INTO dbo.JobCloneTime (BuildId, DefinitionId, Name, Duration, BuildStartTime, JobStartTime, BuildUri, FetchSize, MinFetchSpeed, MaxFetchSpeed, AverageFetchSpeed) VALUES (@BuildId, @DefinitionId, @Name, @Duration, @BuildStartTime, @JobStartTime, @BuildUri, @FetchSize, @MinFetchSpeed, @MaxFetchSpeed, @AverageFetchSpeed)";
            using var command = new SqlCommand(query, transaction.Connection, transaction);
            command.Parameters.AddWithValue("@BuildId", buildId);
            command.Parameters.AddWithValue("@DefinitionId", definitionId);
            command.Parameters.AddWithValue("@Name", jobCloneTime.JobName);
            command.Parameters.AddWithValue("@Duration", jobCloneTime.Duration);
            command.Parameters.AddWithValue("@BuildStartTime", buildStartTime);
            command.Parameters.AddWithValue("@JobStartTime", jobCloneTime.StartTime);
            command.Parameters.AddWithValue("@BuildUri", buildUri.ToString());
            command.Parameters.AddWithValueNullable("@FetchSize", jobCloneTime.FetchSize);
            command.Parameters.AddWithValueNullable("@MinFetchSpeed", jobCloneTime.MinFetchSpeed);
            command.Parameters.AddWithValueNullable("@MaxFetchSpeed", jobCloneTime.MaxFetchSpeed);
            command.Parameters.AddWithValueNullable("@AverageFetchSpeed", jobCloneTime.AverageFetchSpeed);
            var result = await command.ExecuteNonQueryAsync();
            if (result < 0)
            {
                throw new Exception("Unable to execute the insert");
            }
        }

        private async Task UploadBuildCloneTime(SqlTransaction transaction, int buildId, int definitionId, TimeSpan minDuration, TimeSpan maxDuration, DateTimeOffset buildStartTime, Uri buildUri, double? totalFetchSize, double? minFetchSpeed, double? maxFetchSpeed)
        {
            var query = "INSERT INTO dbo.BuildCloneTime (BuildId, DefinitionId, MinDuration, MaxDuration, BuildStartTime, BuildUri, TotalFetchSize, MinFetchSpeed, MaxFetchSpeed) VALUES (@BuildId, @DefinitionId, @MinDuration, @MaxDuration, @BuildStartTime, @BuildUri, @TotalFetchSize, @MinFetchSpeed, @MaxFetchSpeed)";
            using var command = new SqlCommand(query, transaction.Connection, transaction);
            command.Parameters.AddWithValue("@BuildId", buildId);
            command.Parameters.AddWithValue("@DefinitionId", definitionId);
            command.Parameters.AddWithValue("@MinDuration", minDuration);
            command.Parameters.AddWithValue("@MaxDuration", maxDuration);
            command.Parameters.AddWithValue("@BuildStartTime", buildStartTime);
            command.Parameters.AddWithValue("@BuildUri", buildUri.ToString());
            command.Parameters.AddWithValueNullable("@TotalFetchSize", totalFetchSize);
            command.Parameters.AddWithValueNullable("@MinFetchSpeed", minFetchSpeed);
            command.Parameters.AddWithValueNullable("@MaxFetchSpeed", maxFetchSpeed);
            var result = await command.ExecuteNonQueryAsync();
            if (result < 0)
            {
                throw new Exception("Unable to execute the insert");
            }
       }
    }
}
