#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace DevOps.Util.Triage
{
    public sealed class TriageContextUtil
    {
        public TriageContext Context { get; }

        public TriageContextUtil(TriageContext context)
        {
            Context = context;
        }

        public static string GetModelBuildId(BuildKey buildKey) => 
            $"{buildKey.Organization}-{buildKey.Project}-{buildKey.Number}";

        public static GitHubPullRequestKey? GetGitHubPullRequestKey(ModelBuild build) =>
            build.PullRequestNumber.HasValue
                ? (GitHubPullRequestKey?)new GitHubPullRequestKey(build.GitHubOrganization, build.GitHubRepository, build.PullRequestNumber.Value)
                : null;

        public static BuildDefinitionInfo GetBuildDefinitionInfo(ModelBuildDefinition buildDefinition) =>
            new BuildDefinitionInfo(
                buildDefinition.AzureOrganization,
                buildDefinition.AzureProject,
                buildDefinition.DefinitionName,
                buildDefinition.DefinitionId);

        public static BuildKey GetBuildKey(ModelBuild build) =>
            new BuildKey(build.ModelBuildDefinition.AzureOrganization, build.ModelBuildDefinition.AzureProject, build.BuildNumber);

        // TODO: Should be an extension method
        public static BuildInfo GetBuildInfo(ModelBuild build) =>
            new BuildInfo(
                GetBuildKey(build),
                GetBuildDefinitionInfo(build.ModelBuildDefinition),
                build.GitHubOrganization,
                build.GitHubRepository,
                build.PullRequestNumber,
                build.StartTime,
                build.FinishTime,
                build.BuildResult ?? BuildResult.None);

        public ModelBuildDefinition EnsureBuildDefinition(BuildDefinitionInfo definitionInfo)
        {
            var buildDefinition = Context.ModelBuildDefinitions
                .Where(x =>
                    x.AzureOrganization == definitionInfo.Organization &&
                    x.AzureProject == definitionInfo.Project &&
                    x.DefinitionId == definitionInfo.Id)
                .FirstOrDefault();
            if (buildDefinition is object)
            {
                return buildDefinition;
            }

            buildDefinition = new ModelBuildDefinition()
            {
                AzureOrganization = definitionInfo.Organization,
                AzureProject = definitionInfo.Project,
                DefinitionId = definitionInfo.Id,
                DefinitionName = definitionInfo.Name,
            };

            Context.ModelBuildDefinitions.Add(buildDefinition);
            Context.SaveChanges();
            return buildDefinition;
        }

        public async Task<ModelBuild> EnsureBuildAsync(BuildInfo buildInfo)
        {
            var modelBuildId = GetModelBuildId(buildInfo.Key);
            var modelBuild = Context.ModelBuilds
                .Where(x => x.Id == modelBuildId)
                .FirstOrDefault();
            if (modelBuild is object)
            {
                if (modelBuild.BuildResult != buildInfo.BuildResult)
                {
                    modelBuild.StartTime = buildInfo.StartTime;
                    modelBuild.FinishTime = buildInfo.FinishTime;
                    modelBuild.BuildResult = buildInfo.BuildResult;
                    await Context.SaveChangesAsync().ConfigureAwait(false);
                }

                return modelBuild;
            }

            var prKey = buildInfo.PullRequestKey;
            modelBuild = new ModelBuild()
            {
                Id = modelBuildId,
                ModelBuildDefinitionId = EnsureBuildDefinition(buildInfo.DefinitionInfo).Id,
                GitHubOrganization = buildInfo.GitHubInfo?.Organization ?? null,
                GitHubRepository = buildInfo.GitHubInfo?.Repository ?? null,
                PullRequestNumber = prKey?.Number,
                StartTime = buildInfo.StartTime,
                FinishTime = buildInfo.FinishTime,
                BuildNumber = buildInfo.Number,
                BuildResult = buildInfo.BuildResult,
            };
            Context.ModelBuilds.Add(modelBuild);
            Context.SaveChanges();
            return modelBuild;
        }

        public async Task EnsureResult(ModelBuild modelBuild, Build build)
        {
            if (modelBuild.BuildResult != build.Result)
            {
                var buildInfo = build.GetBuildInfo();
                modelBuild.BuildResult = build.Result;
                modelBuild.StartTime = buildInfo.StartTime;
                modelBuild.FinishTime = buildInfo.FinishTime;
                await Context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Determine if this build has already been processed for this query
        /// </summary>
        public bool IsProcessed(ModelTriageIssue modelTriageIssue, ModelBuild modelBuild) =>
            Context.ModelTriageIssueResultCompletes.Any(x =>
                x.ModelTriageIssueId == modelTriageIssue.Id &&
                x.ModelBuildId == modelBuild.Id);

        public bool TryGetTriageIssue(
            SearchKind searchKind, 
            string searchText,
            [NotNullWhen(true)] out ModelTriageIssue? modelTriageIssue)
        {
            modelTriageIssue = Context.ModelTriageIssues
                .Include(x => x.ModelTriageGitHubIssues)
                .Where(x => x.SearchKind == searchKind && x.SearchText == searchText)
                .FirstOrDefault();
            return modelTriageIssue is object;
        }
        public bool TryGetTriageIssue(
            GitHubIssueKey issueKey,
            [NotNullWhen(true)] out ModelTriageIssue? modelTriageIssue)
        {
            var model = Context.ModelTriageGitHubIssues
                .Include(x => x.ModelTriageIssue)
                .Where(x => 
                    x.Organization == issueKey.Organization &&
                    x.Repository == issueKey.Repository && 
                    x.IssueNumber == issueKey.Number)
                .FirstOrDefault();
            if (model is object)
            {
                modelTriageIssue = model.ModelTriageIssue;
                return true;
            }
            else
            {
                modelTriageIssue = null;
                return false;
            }
        }

        public ModelTriageIssue EnsureTriageIssue(
            TriageIssueKind issueKind,
            SearchKind searchKind, 
            string searchText,
            params ModelTriageGitHubIssue[] gitHubIssues)
        {
            if (TryGetTriageIssue(searchKind, searchText, out var modelTriageIssue))
            {
                if (modelTriageIssue.TriageIssueKind != issueKind)
                {
                    modelTriageIssue.TriageIssueKind = issueKind;
                }
            }
            else
            {
                modelTriageIssue = new ModelTriageIssue()
                {
                    TriageIssueKind = issueKind,
                    SearchKind = searchKind,
                    SearchText = searchText,
                    ModelTriageGitHubIssues = new List<ModelTriageGitHubIssue>(),
                };
                Context.ModelTriageIssues.Add(modelTriageIssue);
            }

            foreach (var gitHubIssue in gitHubIssues)
            {
                var existing = modelTriageIssue.ModelTriageGitHubIssues
                    .Where(x => x.IssueKey.IssueUri == gitHubIssue.IssueKey.IssueUri)
                    .FirstOrDefault();
                if (existing is null)
                {
                    modelTriageIssue.ModelTriageGitHubIssues.Add(gitHubIssue);
                }
                else
                {
                    existing.BuildQuery = gitHubIssue.BuildQuery;
                    existing.IncludeDefinitions = gitHubIssue.IncludeDefinitions;
                }
            }

            Context.SaveChanges();

            return modelTriageIssue;
        }

        public List<ModelTriageIssueResult> FindModelTriageIssueResults(ModelTriageIssue triageIssue, ModelTriageGitHubIssue triageGitHubIssue)
        {
            var buildQuery = triageGitHubIssue.BuildQuery ?? "";
            var optionSet = new BuildSearchOptionSet();
            if (optionSet.Parse(buildQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Count != 0)
            {
                throw new Exception("Bad build query");
            }

            if (optionSet.BuildIds.Count > 0 ||
                !string.IsNullOrEmpty(optionSet.Branch) ||
                optionSet.Before.HasValue ||
                optionSet.After.HasValue)
            {
                // Not supported at this time.
                throw new NotSupportedException();
            }

            var project = optionSet.Project ?? DotNetUtil.DefaultAzureProject;

            if (!string.IsNullOrEmpty(optionSet.Repository))
            {
                var both = optionSet.Repository.Split("/");
                var list = Context.ModelTriageIssueResults
                    .Include(x => x.ModelBuild)
                    .ThenInclude(x => x.ModelBuildDefinition)
                    .Where(x => 
                        x.ModelTriageIssueId == triageIssue.Id &&
                        x.ModelBuild.GitHubOrganization == both[0] &&
                        x.ModelBuild.GitHubRepository == both[1])
                    .OrderByDescending(x => x.BuildNumber)
                    .ToList();
                return list;
            }
            else if (optionSet.Definitions.Count > 0)
            {
                var list = new List<ModelTriageIssueResult>();
                foreach (var definition in optionSet.Definitions)
                {
                    if (!DotNetUtil.TryGetDefinitionId(definition, out var definitionProject, out var definitionId))
                    {
                        throw new NotSupportedException();
                    }

                    definitionProject ??= project;
                    var subList = Context.ModelTriageIssueResults
                        .Include(x => x.ModelBuild)
                        .ThenInclude(x => x.ModelBuildDefinition)
                        .Where(x => 
                            x.ModelTriageIssueId == triageIssue.Id &&
                            x.ModelBuild.ModelBuildDefinition.AzureProject == definitionProject &&
                            x.ModelBuild.ModelBuildDefinition.DefinitionId == definitionId)
                        .OrderByDescending(x => x.BuildNumber)
                        .ToList();
                    list.AddRange(subList);
                }

                return list;
            }
            else
            {
                var list = Context.ModelTriageIssueResults
                    .Include(x => x.ModelBuild)
                    .ThenInclude(x => x.ModelBuildDefinition)
                    .Where(x =>
                        x.ModelTriageIssueId == triageIssue.Id &&
                        x.ModelBuild.ModelBuildDefinition.AzureProject == project)
                    .OrderByDescending(x => x.BuildNumber)
                    .ToList();
                return list;
            }
        }

        public List<ModelBuild> FindModelBuilds(ModelTriageIssue modelTriageIssue, ModelTriageGitHubIssue modelTriageGitHubIssue)
        {
            if (string.IsNullOrEmpty(modelTriageGitHubIssue.BuildQuery))
            {
                return FindModelBuildsByRepository(modelTriageIssue, modelTriageGitHubIssue.Organization, modelTriageGitHubIssue.Repository, 100);
            }

            return FindModelBuilds(modelTriageIssue, modelTriageGitHubIssue.BuildQuery);
        }

        public List<ModelBuild> FindModelBuilds(ModelTriageIssue modelTriageIssue, string buildQuery)
        {
            var optionSet = new BuildSearchOptionSet();
            if (optionSet.Parse(buildQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Count != 0)
            {
                throw new Exception("Bad build query");
            }

            return FindModelBuilds(modelTriageIssue, optionSet);
        }

        // TODO: This only supports part of the build query syntax. Could improve this a lot
        public List<ModelBuild> FindModelBuilds(ModelTriageIssue modelTriageIssue, BuildSearchOptionSet optionSet)
        {
            if (optionSet.BuildIds.Count > 0 ||
                !string.IsNullOrEmpty(optionSet.Branch) ||
                optionSet.Before.HasValue ||
                optionSet.After.HasValue)
            {
                // Not supported at this time.
                throw new NotSupportedException();
            }

            var project = optionSet.Project ?? DotNetUtil.DefaultAzureProject;
            var count = optionSet.SearchCount ?? 100;
            var list = new List<ModelBuild>();

            if (!string.IsNullOrEmpty(optionSet.Repository))
            {
                var both = optionSet.Repository.Split("/");
                list.AddRange(FindModelBuildsByRepository(modelTriageIssue, both[0], both[1], count));
            }
            else if (optionSet.Definitions.Count > 0)
            {
                foreach (var definition in optionSet.Definitions)
                {
                    if (!DotNetUtil.TryGetDefinitionId(definition, out var definitionProject, out var definitionId))
                    {
                        throw new NotSupportedException();
                    }

                    definitionProject ??= project;
                    list.AddRange(FindModelBuildsByDefinition(modelTriageIssue, definitionProject, definitionId, count));
                }
            }
            else
            {
                var builds = Context.ModelTriageIssueResults
                    .Include(x => x.ModelBuild)
                    .ThenInclude(x => x.ModelBuildDefinition)
                    .Where(x =>
                        x.ModelTriageIssueId == modelTriageIssue.Id &&
                        x.ModelBuild.ModelBuildDefinition.AzureProject == project)
                    .Select(x => x.ModelBuild)
                    .OrderByDescending(x => x.BuildNumber)
                    .Take(count)
                    .ToList();
                list.AddRange(builds);
            }

            return list;
        }

        public List<ModelBuild> FindModelBuildsByDefinition(ModelTriageIssue modelTriageIssue, string project, int definitionId, int count) =>
            Context.ModelTriageIssueResults
                .Include(x => x.ModelBuild)
                .ThenInclude(x => x.ModelBuildDefinition)
                .Where(x =>
                    x.ModelTriageIssueId == modelTriageIssue.Id &&
                    x.ModelBuild.ModelBuildDefinition.AzureProject == project &&
                    x.ModelBuild.ModelBuildDefinitionId == definitionId)
                .Select(x => x.ModelBuild)
                .OrderByDescending(x => x.BuildNumber)
                .Take(count)
                .ToList();

        public List<ModelBuild> FindModelBuildsByRepository(ModelTriageIssue modelTriageIssue, string organization, string repository, int count) =>
            Context.ModelTriageIssueResults
                .Include(x => x.ModelBuild)
                .Where(x =>
                    x.ModelTriageIssueId == modelTriageIssue.Id &&
                    x.ModelBuild.GitHubOrganization == organization &&
                    x.ModelBuild.GitHubRepository == repository)
                .Select(x => x.ModelBuild)
                .OrderByDescending(x => x.BuildNumber)
                .Take(count)
                .ToList();
    }
}