using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Octokit;
using Org.BouncyCastle.Math.EC.Rfc7748;

namespace DevOps.Util.DotNet.Triage
{
    public static class Extensions
    {
        #region ModelBuild

        public static BuildKey GetBuildKey(this ModelBuild modelBuild) =>
            new BuildKey(
                modelBuild.AzureOrganization,
                modelBuild.AzureProject,
                modelBuild.BuildNumber);

        public static BuildInfo GetBuildInfo(this ModelBuild modelBuild) =>
            new BuildInfo(
                modelBuild.AzureOrganization,
                modelBuild.AzureProject,
                modelBuild.BuildNumber,
                GetGitHubBuildInfo(modelBuild));

        public static BuildAndDefinitionInfo GetBuildAndDefinitionInfo(this ModelBuild modelBuild) =>
            new BuildAndDefinitionInfo(
                modelBuild.AzureOrganization,
                modelBuild.AzureProject,
                modelBuild.BuildNumber,
                modelBuild.DefinitionNumber,
                modelBuild.DefinitionName,
                GetGitHubBuildInfo(modelBuild));

        public static BuildResultInfo GetBuildResultInfo(this ModelBuild modelBuild) =>
            new BuildResultInfo(
                GetBuildAndDefinitionInfo(modelBuild),
                modelBuild.QueueTime,
                modelBuild.StartTime,
                modelBuild.FinishTime,
                modelBuild.BuildResult.ToBuildResult());

        public static GitHubBuildInfo GetGitHubBuildInfo(this ModelBuild modelBuild) =>
            new GitHubBuildInfo(
                modelBuild.GitHubOrganization,
                modelBuild.GitHubRepository,
                modelBuild.PullRequestNumber,
                modelBuild.GitHubTargetBranch);

        public static DefinitionKey GetDefinitionKey(this ModelBuild modelBuild) =>
            new DefinitionKey(
                modelBuild.AzureOrganization,
                modelBuild.AzureProject,
                modelBuild.DefinitionNumber);

        #endregion

        #region ModelBuildAttempt

        public static BuildAttemptKey GetBuildAttemptKey(this ModelBuildAttempt modelBuildAttempt) =>
            new BuildAttemptKey(
                modelBuildAttempt.ModelBuild.GetBuildKey(),
                modelBuildAttempt.Attempt);

        #endregion

        #region ModelBuildDefinition

        public static DefinitionKey GetDefinitionKey(this ModelBuildDefinition modelBuildDefinition) =>
            new DefinitionKey(
                modelBuildDefinition.AzureOrganization,
                modelBuildDefinition.AzureProject,
                modelBuildDefinition.DefinitionNumber);

        public static DefinitionInfo GetDefinitionInfo(this ModelBuildDefinition modelBuildDefinition) =>
            new DefinitionInfo(GetDefinitionKey(modelBuildDefinition), modelBuildDefinition.DefinitionName);

        #endregion

        #region ModelTestResult

        public static HelixLogInfo? GetHelixLogInfo(this ModelTestResult modelTestResult)
        {
            if (!modelTestResult.IsHelixTestResult)
            {
                return null;
            }

            return new HelixLogInfo(
                runClientUri: modelTestResult.HelixRunClientUri,
                consoleUri: modelTestResult.HelixConsoleUri,
                coreDumpUri: modelTestResult.HelixCoreDumpUri,
                testResultsUri: modelTestResult.HelixTestResultsUri);
        }

        public static void SetHelixLogUri(this ModelTestResult modelTestResult, HelixLogKind kind, string uri)
        {
            modelTestResult.IsHelixTestResult = true;
            switch (kind)
            {
                case HelixLogKind.Console:
                    modelTestResult.HelixConsoleUri = uri;
                    break;
                case HelixLogKind.CoreDump:
                    modelTestResult.HelixCoreDumpUri = uri;
                    break;
                case HelixLogKind.RunClient:
                    modelTestResult.HelixRunClientUri = uri;
                    break;
                case HelixLogKind.TestResults:
                    modelTestResult.HelixTestResultsUri = uri;
                    break;
                default:
                    throw new Exception($"Invalid kind '{kind}'");
            }
        }

        #endregion

        #region HelixServer

        public static async Task<List<SearchHelixLogsResult>> SearchHelixLogsAsync(
            this HelixServer helixServer,
            IEnumerable<(BuildInfo BuildInfo, HelixLogInfo HelixLogInfo)> builds,
            SearchHelixLogsRequest request,
            Action<Exception>? onError = null)
        {
            if (request.Text is null)
            {
                throw new ArgumentException("Need text to search for", nameof(request));
            }

            var textRegex = DotNetQueryUtil.CreateSearchRegex(request.Text);

            var list = builds
                .SelectMany(x => request.HelixLogKinds.Select(k => (x.BuildInfo, x.HelixLogInfo, Kind: k, Uri: x.HelixLogInfo.GetUri(k))))
                .Where(x => x.Uri is object)
                .Where(x => x.Kind != HelixLogKind.CoreDump)
                .ToList();
            
            if (list.Count > request.Limit)
            {
                onError?.Invoke(new Exception($"Limiting the {list.Count} logs to first {request.Limit}"));
                list = list.Take(request.Limit).ToList();
            }

            var resultTasks = list
                .AsParallel()
                .Select(async x =>
                {
                    using var stream = await helixServer.DownloadFileAsync(x.Uri!).ConfigureAwait(false);
                    var match = await DotNetQueryUtil.SearchFileForFirstMatchAsync(stream, textRegex, onError).ConfigureAwait(false);
                    var line = match is object && match.Success
                        ? match.Value
                        : null;
                    return (Query: x, Line: line);
                });
            var results = new List<SearchHelixLogsResult>();
            foreach (var task in resultTasks)
            {
                try
                {
                    var result = await task.ConfigureAwait(false);
                    results.Add(new SearchHelixLogsResult(result.Query.BuildInfo, result.Query.Kind, result.Query.Uri!, result.Line));
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                }
            }

            return results;
        }


        #endregion

        #region ModelTrackingIssue

        public static GitHubIssueKey? GetGitHubIssueKey(this ModelTrackingIssue modelTrackingIssue)
        {
            if (modelTrackingIssue is
            {
                GitHubOrganization: { } organization,
                GitHubRepository: { } repository,
                GitHubIssueNumber: int number
            })
            {
                return new GitHubIssueKey(organization, repository, number);
            }

            return null;
        }

        #endregion

        #region ModelGitHubIssue

        public static GitHubIssueKey GetGitHubIssueKey(this ModelGitHubIssue modelGitHubIssue) =>
            new GitHubIssueKey(modelGitHubIssue.Organization, modelGitHubIssue.Repository, modelGitHubIssue.Number);

        #endregion

        #region TrackingIssueUtil

        public static async Task TriageBuildsAsync(this TrackingIssueUtil trackingIssueUtil, ModelTrackingIssue modelTrackingIssue, string extraQuery, CancellationToken cancellationToken = default)
        {
            var attempts = await trackingIssueUtil
                .TriageContextUtil
                .GetModelBuildAttemptsQuery(modelTrackingIssue, extraQuery)
                .Include(x => x.ModelBuild)
                .Select(x => new
                {
                    x.ModelBuild.AzureOrganization,
                    x.ModelBuild.AzureProject,
                    x.ModelBuild.BuildNumber,
                    x.Attempt
                })
                .ToListAsync()
                .ConfigureAwait(false);

            foreach (var attempt in attempts)
            {
                var key = new BuildAttemptKey(attempt.AzureOrganization, attempt.AzureProject, attempt.BuildNumber, attempt.Attempt);
                await trackingIssueUtil.TriageAsync(key, modelTrackingIssue);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        #endregion

        #region Exceptions

        public static SqlException? GetSqlException(this Exception ex)
        {
            var current = ex.InnerException;
            while (current is { })
            {
                if (current is SqlException sqlException)
                {
                    return sqlException;
                }

                current = current.InnerException;
            }

            return null;
        }

        public static bool IsUniqueKeyViolation(this DbUpdateException ex) =>
            GetSqlException(ex) is { } sqlException && sqlException.IsUniqueKeyViolation();

        /// <summary>
        /// Determine if this exception is thrown because of a unique key violation
        /// </summary>
        /// <remarks>
        /// Solution derived from https://entityframework.net/knowledge-base/31515776/how-can-i-catch-uniquekey-violation-exceptions-with-ef6-and-sql-server-
        /// </remarks>
        public static bool IsUniqueKeyViolation(this SqlException ex) => ex.Number switch
        {
            2627 => true,   // Unique constraint error
            547 => true,    // Constraint check violation
            2601 => true,   // Duplicated key row error
            _ => false,
        };

        public static bool IsTimeoutViolation(this SqlException ex) => ex.Number == -2;

        #endregion

        #region Misc

        public static ModelBuildResult ToModelBuildResult(this BuildResult buildResult) =>
            buildResult switch
            {
                BuildResult.None => ModelBuildResult.None,
                BuildResult.Canceled => ModelBuildResult.Canceled,
                BuildResult.Failed => ModelBuildResult.Failed,
                BuildResult.PartiallySucceeded => ModelBuildResult.PartiallySucceeded,
                BuildResult.Succeeded => ModelBuildResult.Succeeded,
                _ => throw new InvalidOperationException(),
            };

        public static BuildResult ToBuildResult(this ModelBuildResult buildResult) =>
            buildResult switch
            {
                ModelBuildResult.None => BuildResult.None,
                ModelBuildResult.Canceled => BuildResult.Canceled,
                ModelBuildResult.Failed => BuildResult.Failed,
                ModelBuildResult.PartiallySucceeded => BuildResult.PartiallySucceeded,
                ModelBuildResult.Succeeded => BuildResult.Succeeded,
                _ => throw new InvalidOperationException(),
            };

        public static IssueType ToIssueType(this ModelIssueType issueType) =>
            issueType switch
            {
                ModelIssueType.Unknown => IssueType.Unknown,
                ModelIssueType.Warning => IssueType.Warning,
                ModelIssueType.Error => IssueType.Error,
                _ => throw new InvalidOperationException(),
            };

        public static ModelIssueType ToModelIssueType(this IssueType issueType) =>
            issueType switch
            {
                IssueType.Unknown => ModelIssueType.Unknown,
                IssueType.Warning => ModelIssueType.Warning,
                IssueType.Error => ModelIssueType.Error,
                _ => throw new InvalidOperationException(),
            };

        public static async Task<List<BuildResultInfo>> ToBuildResultInfoListAsync(this IQueryable<ModelBuild> query)
        {
            var results = await query
                .Select(x => new
                {
                    x.AzureOrganization,
                    x.AzureProject,
                    x.GitHubOrganization,
                    x.GitHubRepository,
                    x.GitHubTargetBranch,
                    x.PullRequestNumber,
                    x.BuildNumber,
                    x.BuildResult,
                    x.DefinitionName,
                    x.DefinitionNumber,
                    x.QueueTime,
                    x.StartTime,
                    x.FinishTime,
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var list = new List<BuildResultInfo>(results.Count);
            foreach (var result in results)
            {
                var buildInfo = new BuildResultInfo(
                    new BuildAndDefinitionInfo(
                        result.AzureOrganization,
                        result.AzureProject,
                        result.BuildNumber,
                        result.DefinitionNumber,
                        result.DefinitionName,
                        new GitHubBuildInfo(result.GitHubOrganization, result.GitHubRepository, result.PullRequestNumber, result.GitHubTargetBranch)),
                    result.QueueTime,
                    result.StartTime,
                    result.FinishTime,
                    result.BuildResult.ToBuildResult());
                list.Add(buildInfo);
            }

            return list;
        }

        public static string GetDisplayString(this ModelBuildKind kind) => kind switch
        {
            ModelBuildKind.All => "All",
            ModelBuildKind.MergedPullRequest => "Merged Pull Request",
            ModelBuildKind.PullRequest => "Pull Request",
            ModelBuildKind.Rolling => "Rolling",
            _ => throw new InvalidOperationException($"Unexpected value: {kind}")
        };

        public static GitHubIssueKey GetIssueKey(this Octokit.Issue issue)
        {
            var regex = new Regex(@"https://github.com/([\w\d-]+)/([\w\d-]+)/issues/\d+");
            var match = regex.Match(issue.HtmlUrl.ToString());
            if (!match.Success)
            {
                throw new Exception("Cannot parse GitHub issue URL");
            }

            return new GitHubIssueKey(
                match.Groups[1].Value,
                match.Groups[2].Value,
                issue.Number);
        }
        #endregion
    }
}