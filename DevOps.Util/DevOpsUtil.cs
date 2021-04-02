using DevOps.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DevOps.Util
{
    public static class DevOpsUtil
    {
        public static TestOutcome[] FailedTestOutcomes => new[]
        {
            TestOutcome.Failed,
            TestOutcome.Aborted
        };

        public static BuildKey GetBuildKey(Build build)
        {
            var organization = GetOrganization(build);
            return new BuildKey(organization, build.Project.Name, build.Id);
        }

        public static BuildKey GetBuildKey(BuildResultInfo buildInfo) =>
            new BuildKey(buildInfo.Organization, buildInfo.Project, buildInfo.Number);

        public static DefinitionInfo GetDefinitionInfo(Build build)
        {
            var organization = GetOrganization(build);
            return new DefinitionInfo(
                organization,
                build.Definition.Project.Name,
                build.Definition.Id,
                build.Definition.Name);
        }

        public static BuildResultInfo GetBuildResultInfo(Build build)
        {
            var buildAndDefinitionInfo = GetBuildAndDefinitionInfo(build);
            var queueTime = build.GetQueueTime()?.UtcDateTime ?? throw new InvalidOperationException();
            var startTime = build.GetStartTime()?.UtcDateTime ?? throw new InvalidOperationException();
            var finishTime = build.GetFinishTime()?.UtcDateTime;
            var gitHubBuildInfo = GetGitHubBuildInfo(build);
            return new BuildResultInfo(buildAndDefinitionInfo, queueTime, startTime, finishTime, build.Result);
        }

        public static BuildInfo GetBuildInfo(Build build)
        {
            var buildKey = GetBuildKey(build);
            return new BuildInfo(
                buildKey.Organization,
                buildKey.Project,
                buildKey.Number,
                GetGitHubBuildInfo(build));
        }

        public static BuildAndDefinitionInfo GetBuildAndDefinitionInfo(Build build)
        {
            var buildKey = GetBuildKey(build);
            return new BuildAndDefinitionInfo(
                buildKey.Organization,
                buildKey.Project,
                buildKey.Number,
                build.Definition.Id,
                build.Definition.Name,
                GetGitHubBuildInfo(build));
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

        public static string GetDefinitionUri(string organization, string project, int definitionId) =>
             $"https://{organization}.visualstudio.com/{project}/_build?definitionId={definitionId}";

        public static string GetDefinitionUri(Build build) =>
            GetDefinitionUri(
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

        /// <summary>
        /// This will return the target branch for a given build. For a pull request this will be the branch
        /// the code wants to merge to. For other build types it will be the branch they were against
        /// </summary>
        public static string? GetTargetBranch(Build build)
        {
            try
            {
                if (build.Reason == BuildReason.PullRequest)
                {
                    dynamic d = JValue.Parse(build.Parameters);
                    var target = d["system.pullRequest.targetBranch"];
                    return target;
                }

                const string prefix = "refs/heads/";
                if (build.SourceBranch is string &&
                    build.SourceBranch.StartsWith(prefix))
                {
                    return build.SourceBranch.Substring(prefix.Length);
                }

                // One valid way to end up here is when dealing with manual builds. These can be 
                // against arbitrary commits, branch names, etc ... They aren't necessarily an attempt
                // to merge into the repository hence won't always have a target branch
                return null;
            }
            catch
            {
                return null;
            }
        }

        public static GitHubBuildInfo? GetGitHubBuildInfo(Build build)
        {
            if (GetRepositoryInfo(build) is { } repositoryInfo &&
                repositoryInfo.TryGetGitHubInfo(out var organization, out var repository))
            {
                int? prNumber = null;
                if (build.Reason == BuildReason.PullRequest &&
                    build.SourceBranch is object)
                {
                    var items = build.SourceBranch.Split('/');
                    if (items.Length > 2 && int.TryParse(items[2], out int number))
                    {
                        prNumber = number;
                    }
                }

                var targetBranch = GetTargetBranch(build);
                return new GitHubBuildInfo(organization, repository, prNumber, targetBranch);
            }

            return null;
        }

        public static GitHubPullRequestKey? GetPullRequestKey(Build build) => GetGitHubBuildInfo(build)?.PullRequestKey;

        public static bool TryGetPullRequestKey(Build build, out GitHubPullRequestKey prKey)
        {
            if (GetPullRequestKey(build) is { } k)
            {
                prKey = k;
                return true;
            }
            else
            {
                prKey = default;
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
