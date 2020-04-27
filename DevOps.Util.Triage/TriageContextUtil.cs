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

        public static GitHubIssueKey GetGitHubIssueKey(ModelTimelineQuery timelineQuery) =>
            new GitHubIssueKey(timelineQuery.GitHubOrganization, timelineQuery.GitHubRepository, timelineQuery.IssueNumber);

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

        public static BuildInfo GetBuildInfo(ModelBuild build) =>
            new BuildInfo(
                GetBuildKey(build),
                GetBuildDefinitionInfo(build.ModelBuildDefinition),
                build.GitHubOrganization,
                build.GitHubRepository,
                build.PullRequestNumber,
                build.StartTime,
                build.FinishTime);

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

        public ModelBuild EnsureBuild(BuildInfo buildInfo)
        {
            var modelBuildId = GetModelBuildId(buildInfo.Key);
            var modelBuild = Context.ModelBuilds
                .Where(x => x.Id == modelBuildId)
                .FirstOrDefault();
            if (modelBuild is object)
            {
                return modelBuild;
            }

            var prKey = buildInfo.PullRequestKey;
            modelBuild = new ModelBuild()
            {
                Id = modelBuildId,
                ModelBuildDefinitionId = EnsureBuildDefinition(buildInfo.DefinitionInfo).Id,
                GitHubOrganization = prKey?.Organization,
                GitHubRepository = prKey?.Repository,
                PullRequestNumber = prKey?.Number,
                StartTime = buildInfo.StartTime,
                FinishTime = buildInfo.FinishTime,
                BuildNumber = buildInfo.Number,
            };
            Context.ModelBuilds.Add(modelBuild);
            Context.SaveChanges();
            return modelBuild;
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
                .Where(x => x.SearchKind == searchKind || x.SearchText == searchText)
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

        public void EnsureTriageIssue(
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
                    Context.SaveChanges();
                }
            }
            else
            {
                modelTriageIssue = new ModelTriageIssue()
                {
                    TriageIssueKind = issueKind,
                    SearchKind = searchKind,
                    SearchText = searchText,
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
            }

            Context.SaveChanges();
        }
    }
}