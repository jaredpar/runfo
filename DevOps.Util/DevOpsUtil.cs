using DevOps.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DevOps.Util
{
    public static class DevOpsUtil
    {
        public static BuildKey GetBuildKey(Build build)
        {
            var organization = GetOrganization(build);
            return new BuildKey(organization, build.Project.Name, build.Id);
        }

        public static BuildKey GetBuildKey(BuildInfo buildInfo) =>
            new BuildKey(buildInfo.Organization, buildInfo.Project, buildInfo.Number);

        public static BuildDefinitionInfo GetBuildDefinitionInfo(Build build)
        {
            var organization = GetOrganization(build);
            return new BuildDefinitionInfo(
                organization,
                build.Definition.Project.Name,
                build.Definition.Id,
                build.Definition.Name);
        }

        public static BuildInfo GetBuildInfo(Build build)
        {
            var key = GetBuildKey(build);
            var definitionInfo = GetBuildDefinitionInfo(build);
            var startTime = build.GetStartTime()?.UtcDateTime;
            var finishTime = build.GetFinishTime()?.UtcDateTime;
            if (TryGetPullRequestKey(build, out var pullRequestKey))
            {
                return new BuildInfo(key, definitionInfo, pullRequestKey, startTime, finishTime, build.Result);
            }

            var gitHubInfo = GetGitHubInfo(build);
            return new BuildInfo(key, definitionInfo, gitHubInfo, startTime, finishTime, build.Result);
        }

        public static bool TryParseBuildKey(Uri uri, out BuildKey buildKey)
        {
            var regex = new Regex(@"https://dev.azure.com/(\w+)/(\w+)/.*buildId=(\d+)");
            var match = regex.Match(uri.ToString());
            if (match.Success && int.TryParse(match.Groups[3].Value, out var buildId))
            {
                buildKey = new BuildKey(match.Groups[1].Value, match.Groups[2].Value, buildId);
                return true;
            }

            buildKey = default;
            return false;
        }

        public static string GetBuildDefinitionUri(string organization, string project, int definitionId) =>
             $"https://{organization}.visualstudio.com/{project}/_build?definitionId={definitionId}";

        public static string GetBuildDefinitionUri(Build build) =>
            GetBuildDefinitionUri(
                GetOrganization(build),
                build.Project.Name,
                build.Definition.Id);

        public static string GetOrganization(Build build)
        {
            var uri = new Uri(build.Url);
            if (uri.Host == "dev.azure.com")
            {
                return uri.PathAndQuery.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)[0];
            }
            else
            {
                return uri.Host.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)[0];
            }
        }

        /// <summary>
        /// Get a human readable build URI for the build
        /// </summary>
        public static string GetBuildUri(Build build)
        {
            var organization = GetOrganization(build);
            return GetBuildUri(organization, build.Project.Name, build.Id);
        }

        /// <summary>
        /// Get a human readable build URI for the build
        /// </summary>
        public static string GetBuildUri(string organization, string project, int buildId) =>
            $"https://dev.azure.com/{organization}/{project}/_build/results?buildId={buildId}";

        public static GitHubInfo? GetGitHubInfo(Build build) =>
            GetRepositoryInfo(build) is { } repositoryInfo
            ? GitHubInfo.TryCreate(repositoryInfo)
            : null;

        public static RepositoryInfo? GetRepositoryInfo(Build build)
        {
            var repo = build.Repository;
            if (repo is object &&
                repo.Id is object &&
                repo.Type is object)
            {
                return new RepositoryInfo(repo.Id, repo.Type);
            }

            return null;
        }

        public static GitHubPullRequestKey? GetPullRequestKey(Build build) =>
            TryGetPullRequestKey(build, out var pullRequestKey)
                ? (GitHubPullRequestKey?)pullRequestKey
                : null;

        public static bool TryGetPullRequestKey(Build build, out GitHubPullRequestKey prKey)
        {
            prKey = default;
            if (build.Reason != BuildReason.PullRequest)
            {
                return false;
            }

            try
            {
                var items = build.SourceBranch.Split('/');
                if (int.TryParse(items[2], out int number) &&
                    GetGitHubInfo(build) is GitHubInfo gitHubInfo)
                {
                    prKey = new GitHubPullRequestKey(gitHubInfo.Organization, gitHubInfo.Repository, number);
                    return true;
                }
            }
            catch
            {

            }

            return false;
        }

        public static bool TryGetPullRequestNumber(Build build, out int number)
        {
            if (TryGetPullRequestKey(build, out var pullRequestKey))
            {
                number = pullRequestKey.Number;
                return true;
            }
            else
            {
                number = 0;
                return false;
            }
        }

        /// <summary>
        /// Returns the uncompressed byte size of the artifact
        /// </summary>
        public static int? GetArtifactByteSize(BuildArtifact buildArtifact)
        {
            if (buildArtifact.Resource is object &&
                buildArtifact.Resource.Properties is object)
            {
                try
                {
                    dynamic properties = buildArtifact.Resource.Properties;
                    return (int)properties.artifactsize;
                }
                catch
                {
                    // Okay if dynamic information is not available
                    return null;
                }
            }

            return null;
        }

        public static BuildArtifactKind GetArtifactKind(BuildArtifact buildArtifact)
        {
            if (buildArtifact.Resource is object &&
                buildArtifact.Resource.Type is object)
            {
                try
                {
                    return (BuildArtifactKind)Enum.Parse(typeof(BuildArtifactKind), buildArtifact.Resource.Type);
                }
                catch
                {
                    return BuildArtifactKind.Unknown;
                }
            }

            return BuildArtifactKind.Unknown;
        }

        public static DateTimeOffset? ConvertFromRestTime(string time)
        {
            if (time is null || !DateTime.TryParse(time, out var dateTime))
            {
                return null;
            }

            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            return new DateTimeOffset(dateTime);
        }

        public static string ConvertToRestTime(DateTimeOffset dateTime) => dateTime.UtcDateTime.ToString("o");
    }
}
