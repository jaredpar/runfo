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

namespace Scratch
{
    /// <summary>
    /// Used to migrate data from the old schema to the new one
    /// </summary>
    public sealed class MigrateUtil
    {
        public Dictionary<(ModelMigrationKind, int), int> MigrationCache = new();
        public DotNetQueryUtil DotNetQueryUtil { get; }
        public TriageContextUtil TriageContextUtil { get; }
        public ModelDataUtil ModelDataUtil { get; }
        public TriageContext TriageContext => TriageContextUtil.Context;

        public MigrateUtil(
            DotNetQueryUtil queryUtil,
            TriageContextUtil triageContextUtil,
            ILogger logger)
        {
            DotNetQueryUtil = queryUtil;
            TriageContextUtil = triageContextUtil;
            ModelDataUtil = new ModelDataUtil(queryUtil, triageContextUtil, logger);
        }

        private async Task SaveNewId(ModelMigrationKind kind, int oldId, int newId)
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

        private async Task<int?> GetNewId(ModelMigrationKind kind, int oldId)
        {
            if (MigrationCache.TryGetValue((kind, oldId), out var newId))
            {
                return newId;
            }

            var model = await TriageContext
                .ModelMigrations
                .Where(x => x.MigrationKind == kind && x.OldId == oldId)
                .FirstOrDefaultAsync();
            if (model is object)
            {
                MigrationCache[(kind, oldId)] = model.NewId;
                return model.NewId;
            }

            return null;
        }

        public async Task Migrate(string migrateDirectory)
        {
            await MigrateDefinitions(migrateDirectory);
            await MigrateTrackingIssues(migrateDirectory);
        }

        private async Task MigrateDefinitions(string migrateDirectory)
        {
            foreach (var line in File.ReadAllLines(Path.Combine(migrateDirectory, "definitions.csv")))
            {
                var items = line.Split(',');
                var oldId = int.Parse(items[0]);
                if (await GetNewId(ModelMigrationKind.Definition, oldId) is object)
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

        private async Task MigrateTrackingIssues(string migrateDirectory)
        {
            foreach (var line in File.ReadAllLines(Path.Combine(migrateDirectory, "tracking-issues.csv")))
            {
                var items = line.Split(',');
                var oldId = int.Parse(items[0]);
                if (await GetNewId(ModelMigrationKind.TrackingIssue, oldId) is object)
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

        private static string ParseString(string s) => s == "NULL" ? "" : s;
        private static int? ParseNumber(string s) => s == "NULL" ? null : int.Parse(s);
    }
}
