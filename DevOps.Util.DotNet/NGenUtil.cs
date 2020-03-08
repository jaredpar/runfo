using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet
{
    public sealed class NGenUtil : IDisposable
    {
        public const string ProjectName = "DevDiv";
        public const int RoslynSignedBuildDefinitionId = 8972;

        public SqlConnection SqlConnection { get; }
        public DevOpsServer DevOpsServer { get; }
        public ILogger Logger { get; }

        public NGenUtil(string personalAccessToken, string connectionString, ILogger logger = null)
        {
            Logger = logger ?? DotNetUtil.CreateConsoleLogger();
            DevOpsServer = new DevOpsServer("devdiv", personalAccessToken);
            SqlConnection = new SqlConnection(connectionString);
        }

        public void Dispose()
        {
            SqlConnection.Dispose();
        }

        public async Task<List<Build>> ListBuildsAsync(int? top = null) =>
            await DevOpsServer.ListBuildsAsync(ProjectName, definitions: new[] { RoslynSignedBuildDefinitionId }, top: top, queryOrder: BuildQueryOrder.FinishTimeDescending);

        public async Task<bool> IsBuildUploadedAsync(int buildId)
        {
            await SqlConnection.EnsureOpenAsync();
            var query = "SELECT * FROM dbo.NGenAssemblyData WHERE BuildId = @BuildId";
            using var command = new SqlCommand(query, SqlConnection);
            command.Parameters.AddWithValue("@BuildId", buildId);
            using var reader = await command.ExecuteReaderAsync();
            return reader.HasRows;
        }

        public bool CanBeUploaded(Build build) =>
            build.Result == BuildResult.Succeeded ||
            build.Result == BuildResult.PartiallySucceeded;

        private void VerifyBuild(Build build)
        {
            if (!CanBeUploaded(build))
            {
                throw new InvalidOperationException("The build didn't succeed");
            }
        }

        public async Task UploadBuild(int buildId) => await DevOpsServer.GetBuildAsync(ProjectName, buildId);

        public async Task UploadBuild(Build build)
        {
            VerifyBuild(build);
            var uri = DevOpsUtil.GetBuildUri(build).ToString();
            Logger.LogInformation($"Uploading {build.Id} - {uri}");

            if (await IsBuildUploadedAsync(build.Id))
            {
                Logger.LogInformation("Build already uploaded");
                return;
            }

            var branchName = DotNetUtil.NormalizeBranchName(build.SourceBranch);
            var list = await GetNGenAssemblyDataAsync(build);
            await DotNetUtil.DoWithTransactionAsync(SqlConnection, $"Uploading {build.Id}", async transaction =>
            {
                foreach (var ngenAssemblyData in list)
                {
                    await UploadNGenAssemblyDataAsync(transaction, build.Id, branchName, uri, ngenAssemblyData);
                }
            });
        }

        public async Task<List<NGenAssemblyData>> GetNGenAssemblyDataAsync(int buildId) =>
            await GetNGenAssemblyDataAsync(await DevOpsServer.GetBuildAsync(ProjectName, buildId));

        public async Task<List<NGenAssemblyData>> GetNGenAssemblyDataAsync(Build build)
        {
            VerifyBuild(build);

            var uri = DevOpsUtil.GetBuildUri(build);
            Logger.LogInformation($"Processing {build.Id} - {uri}");

            // Newer builds have the NGEN logs in a separate artifact altogether to decrease the time needed 
            // to downloaad them. Try that first and fall back to the diagnostic logs if it doesn't exist.
            MemoryStream stream;
            Func<ZipArchiveEntry, bool> predicate;

            try
            {
                Logger.LogInformation("Downloading NGEN logs");
                stream = await DevOpsServer.DownloadArtifactAsync(ProjectName, build.Id, "NGen Logs");
                predicate = e => !string.IsNullOrEmpty(e.Name);
            }
            catch (Exception)
            {
                Logger.LogInformation("Falling back to diagnostic logs");
                stream = await DevOpsServer.DownloadArtifactAsync(ProjectName, build.Id, "Build Diagnostic Files");
                predicate = e => !string.IsNullOrEmpty(e.Name) && e.FullName.StartsWith("Build Diagnostic Files/ngen/");
            }

            return await GetFromStream(stream, predicate);
        }

        private async Task<List<NGenAssemblyData>> GetFromStream(Stream stream, Func<ZipArchiveEntry, bool> predicate)
        { 
            var regex = new Regex(@"(.*)-([\w.]+).ngen.txt", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            using var zipArchive = new ZipArchive(stream);
            var list = new List<NGenAssemblyData>();
            foreach (var entry in zipArchive.Entries)
            {
                if (predicate(entry))
                {
                    var match = regex.Match(entry.Name);
                    var assemblyName = match.Groups[1].Value;
                    var targetFramework = match.Groups[2].Value;
                    var methodList = new List<string>();
                    using var entryStream = entry.Open();
                    using var reader = new StreamReader(entryStream);

                    do
                    {
                        var line = await reader.ReadLineAsync();
                        if (line is null)
                        {
                            break;
                        }

                        methodList.Add(line);
                    }
                    while (true);

                    var ngenAssemblyData = new NGenAssemblyData(assemblyName, targetFramework, methodList.Count);
                    list.Add(ngenAssemblyData);
                }
            }

            return list;
        }

        private async Task UploadNGenAssemblyDataAsync(SqlTransaction transaction, int buildId, string branchName, string buildUri, NGenAssemblyData ngenAssemblyData)
        {
            var query = "INSERT INTO dbo.NGenAssemblyData (BuildId, BranchName, BuildUri, AssemblyName, TargetFramework, MethodCount) VALUES (@BuildId, @BranchName, @BuildUri, @AssemblyName, @TargetFramework, @MethodCount)";
            using var command = new SqlCommand(query, transaction.Connection, transaction);
            command.Parameters.AddWithValue("@BuildId", buildId);
            command.Parameters.AddWithValue("@BranchName", branchName);
            command.Parameters.AddWithValue("@BuildUri", buildUri);
            command.Parameters.AddWithValue("@AssemblyName", ngenAssemblyData.AssemblyName);
            command.Parameters.AddWithValue("@TargetFramework", ngenAssemblyData.TargetFramework);
            command.Parameters.AddWithValue("@MethodCount", ngenAssemblyData.MethodCount);
            var result = await command.ExecuteNonQueryAsync();
            if (result < 0)
            {
                throw new Exception("Unable to execute the insert");
            }
        }
    }
}
