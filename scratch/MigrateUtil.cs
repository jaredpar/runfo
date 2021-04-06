using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using DevOps.Util;
using System.Net.Http;

namespace Scratch
{
    /// <summary>
    /// Used to migrate data from the old schema to the new one
    /// </summary>
    public sealed class MigrateUtil
    {
        public Dictionary<(ModelMigrationKind, int), int?> MigrationCache = new();
        public DotNetQueryUtil DotNetQueryUtil { get; }
        public TriageContextUtil TriageContextUtil { get; }
        public ModelDataUtil ModelDataUtil { get; }
        public TriageContext TriageContext => TriageContextUtil.Context;
        public DevOpsServer DevOpsServer => DotNetQueryUtil.Server;

        public MigrateUtil(
            DotNetQueryUtil queryUtil,
            TriageContextUtil triageContextUtil,
            ILogger logger)
        {
            DotNetQueryUtil = queryUtil;
            TriageContextUtil = triageContextUtil;
            ModelDataUtil = new ModelDataUtil(queryUtil, triageContextUtil, logger);
        }

        private async Task SaveNewId(ModelMigrationKind kind, int oldId, int? newId)
        {
            var model = new ModelMigration()
            {
                MigrationKind = kind,
                OldId = oldId,
                NewId = newId
            };
            TriageContext.ModelMigrations.Add(model);
            await TriageContext.SaveChangesAsync();
            MigrationCache[(kind, oldId)] = newId;
        }

        private async Task<(bool Succeeded, int? NewId)> TryGetNewId(ModelMigrationKind kind, int oldId)
        {
            if (MigrationCache.TryGetValue((kind, oldId), out var newId))
            {
                return (true, newId);
            }

            var model = await TriageContext
                .ModelMigrations
                .Where(x => x.MigrationKind == kind && x.OldId == oldId)
                .FirstOrDefaultAsync();
            if (model is object)
            {
                MigrationCache[(kind, oldId)] = model.NewId;
                return (true, model.NewId);
            }

            return (false, null);
        }

        private async Task<int?> GetNewId(ModelMigrationKind kind, int oldId)
        {
            var result = await TryGetNewId(kind, oldId);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException();
            }

            return result.NewId;
        }

        public async Task Migrate(string migrateDirectory)
        {
            await MigrateDefinitionsAsync(migrateDirectory);
            await MigrateTrackingIssuesAsync(migrateDirectory);
            await MigrateTrackingIssueMatchesAsync(migrateDirectory);
            await MigrateTrackingIssueResultsAsync(migrateDirectory);
        }

        private async Task MigrateDefinitionsAsync(string migrateDirectory)
        {
            foreach (var line in File.ReadAllLines(Path.Combine(migrateDirectory, "definitions.csv")))
            {
                var items = line.Split(',');
                var oldId = int.Parse(items[0]);
                if (await TryGetNewId(ModelMigrationKind.Definition, oldId) is (true, _))
                {
                    continue;
                }

                var definitionInfo = new DefinitionInfo(
                    new DefinitionKey(
                        items[1],
                        items[2],
                        int.Parse(items[4])),
                    items[3]);
                Console.WriteLine($"Migrating {definitionInfo.DefinitionKey}");
                var definition = await TriageContextUtil.EnsureBuildDefinitionAsync(definitionInfo);
                await SaveNewId(ModelMigrationKind.Definition, oldId, definition.Id);
            }
        }

        private async Task MigrateTrackingIssuesAsync(string migrateDirectory)
        {
            foreach (var line in File.ReadAllLines(Path.Combine(migrateDirectory, "tracking-issues.csv")))
            {
                var items = line.Split(',');
                var oldId = int.Parse(items[0]);
                if (await TryGetNewId(ModelMigrationKind.TrackingIssue, oldId) is (true, _))
                {
                    continue;
                }

                var definitionId = ParseNumber(items[7]) is { } oldDefinitionId
                    ? await GetNewId(ModelMigrationKind.Definition, oldDefinitionId)
                    : null;
                var isActive = items[3] == "1";
                if (!isActive)
                {
                    continue;
                }

                var model = new ModelTrackingIssue()
                {
                    TrackingKind = Enum.Parse<TrackingKind>(items[1]),
                    SearchQuery = items[2],
                    IsActive = true,
                    GitHubOrganization = ParseString(items[4]),
                    GitHubRepository = ParseString(items[5]),
                    GitHubIssueNumber = ParseNumber(items[6]),
                    IssueTitle = items[8],
                    ModelBuildDefinitionId = definitionId,
                };

                if (string.IsNullOrEmpty(model.GitHubOrganization))
                {
                    continue;
                }

                Console.WriteLine($"Migrating {model.IssueTitle}");
                TriageContext.ModelTrackingIssues.Add(model);
                await TriageContext.SaveChangesAsync();
                await SaveNewId(ModelMigrationKind.TrackingIssue, oldId, model.Id);
            }
        }

        private async Task MigrateTrackingIssueMatchesAsync(string migrateDirectory)
        {
            foreach (var line in File.ReadAllLines(Path.Combine(migrateDirectory, "tracking-issue-matches.csv")))
            {
                var items = line.Split(',');
                var oldId = int.Parse(items[0]);
                if (await TryGetNewId(ModelMigrationKind.TrackingIssueMatch, oldId) is (true, _))
                {
                    continue;
                }

                if (await EnsureModelBuildAttemptIdAsync(
                    int.Parse(items[2]),
                    GetBuildKey(items[3]),
                    int.Parse(items[4])) is not { } attemptId)
                {
                    continue;
                }

                var model = new ModelTrackingIssueMatch()
                {
                    ModelBuildAttemptId = attemptId,
                    HelixLogUri = ParseString(items[5]),
                    JobName = ParseString(items[6]),
                    HelixLogKind = Enum.Parse<HelixLogKind>(items[7]),
                };

                Console.WriteLine($"Migrating tracking match {oldId}");
                TriageContext.ModelTrackingIssueMatches.Add(model);
                await TriageContext.SaveChangesAsync();
                await SaveNewId(ModelMigrationKind.TrackingIssueMatch, oldId, model.Id);
            }
        }

        private async Task MigrateTrackingIssueResultsAsync(string migrateDirectory)
        {
            foreach (var line in File.ReadAllLines(Path.Combine(migrateDirectory, "tracking-issue-results.csv")))
            {
                var items = line.Split(',');
                var oldId = int.Parse(items[0]);
                if (await TryGetNewId(ModelMigrationKind.TrackingIssueResult, oldId) is (true, _))
                {
                    continue;
                }

                if (await EnsureModelBuildAttemptIdAsync(int.Parse(items[2]), GetBuildKey(items[3]), int.Parse(items[4])) is not { } attemptId)
                {
                    continue;
                }

                var model = new ModelTrackingIssueResult()
                {
                    IsPresent = true,
                    ModelTrackingIssueId = await GetNewId(ModelMigrationKind.TrackingIssue, int.Parse(items[1])) ?? throw new Exception("Missing tracking issue"),
                    ModelBuildAttemptId = attemptId,
                };

                Console.WriteLine($"Migrating tracking result {oldId}");
                TriageContext.ModelTrackingIssueResults.Add(model);
                await TriageContext.SaveChangesAsync();
                await SaveNewId(ModelMigrationKind.TrackingIssueResult, oldId, model.Id);
            }
        }

        private async Task<int?> EnsureModelBuildAttemptIdAsync(int oldId, BuildKey buildKey, int attempt)
        {
            if (await TryGetNewId(ModelMigrationKind.BuildAttempt, oldId) is (true, var newId))
            {
                return newId;
            }

            var modelBuildAttempt = await EnsureBuildAttemptAsync(buildKey, attempt);
            newId = modelBuildAttempt?.Id;
            await SaveNewId(ModelMigrationKind.BuildAttempt, oldId, newId);
            return newId;
        }

        private async Task<ModelBuildAttempt?> EnsureBuildAttemptAsync(BuildKey buildKey, int attempt)
        {
            var modelBuildAttempt = await TriageContextUtil.FindModelBuildAttemptAsync(new(buildKey, attempt));
            if (modelBuildAttempt is object)
            {
                return modelBuildAttempt;
            }

            Console.WriteLine($"Getting build {buildKey}");
            try
            {
                var build = await DevOpsServer.GetBuildAsync(buildKey.Project, buildKey.Number);

                var buildAttemptKey = await ModelDataUtil.EnsureModelInfoAsync(build, includeTests: false);
                if (buildAttemptKey.Attempt < attempt)
                {
                    throw new InvalidOperationException("Missing attempt");
                }

                return await TriageContextUtil.GetModelBuildAttemptAsync(buildAttemptKey);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Skipping {buildKey}: {ex.Message}");
                return null;
            }
        }

        private static BuildKey GetBuildKey(string key)
        {
            var parts = key.Split('-');
            return new BuildKey(parts[0], parts[1], int.Parse(parts[2]));
        }
        private static string ParseString(string s) => s == "NULL" ? "" : s;
        private static int? ParseNumber(string s) => s == "NULL" ? null : int.Parse(s);
    }
}
