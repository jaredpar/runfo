#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octokit;

namespace DevOps.Util.DotNet
{
    public sealed class GitHubUtil
    {
        public IGitHubClient GitHubClient { get; }

        public GitHubUtil(IGitHubClient gitHubClient)
        {
            GitHubClient = gitHubClient;
        }

        public void EnsureGitHubAuthenticatiod()
        {
            if (GitHubClient.Connection.Credentials == Credentials.Anonymous)
            {
                throw new InvalidOperationException("Need GitHub credentials to proceed");
            }
        }

        public async IAsyncEnumerable<PullRequest> EnumerateClosedPullRequests(
            string organization,
            string repository)
        {
            EnsureGitHubAuthenticatiod();
            var apiConnection = new ApiConnection(GitHubClient.Connection);
            var pullRequestsClient = new PullRequestsClient(apiConnection);
            var page = 1;

            while (true)
            {
                var apiOptions = new ApiOptions()
                {
                    StartPage = page,
                    PageCount = 1
                };

                var pullRequests = await pullRequestsClient.GetAllForRepository(organization, repository, new PullRequestRequest()
                {
                    State = ItemStateFilter.Closed,
                    SortDirection = SortDirection.Descending,
                    SortProperty = PullRequestSort.Created,
                }, apiOptions).ConfigureAwait(false);

                if (pullRequests.Count == 0)
                {
                    break;
                }

                foreach (var pullRequest in pullRequests)
                {
                    yield return pullRequest;
                }

                page++;
            }
        }

        public async IAsyncEnumerable<PullRequest> EnumerateMergedPullRequests(
            string organization,
            string repository)
        {
            EnsureGitHubAuthenticatiod();
            await foreach (var pullRequest in EnumerateClosedPullRequests(organization, repository).ConfigureAwait(false))
            {
                if (pullRequest.Merged)
                {
                    yield return pullRequest;
                }
            }
        }

        public async IAsyncEnumerable<(PullRequest PullReuqest, Build Build)> EnumerateMergedPullRequestBuilds(
            DevOpsServer server,
            GitHubInfo gitHubInfo,
            string project,
            int[]? definitions)
        {
            await foreach (var pullRequest in EnumerateClosedPullRequests(gitHubInfo.Organization, gitHubInfo.Repository).ConfigureAwait(false))
            {
                var prKey = new GitHubPullRequestKey(gitHubInfo.Organization, gitHubInfo.Repository, pullRequest.Number);
                Build? build = null;
                try
                {
                    var builds = (await server.ListPullRequestBuildsAsync(prKey, project, definitions).ConfigureAwait(false))
                        .OrderByDescending(b => b.BuildNumber)
                        .Where(x => x.Status == BuildStatus.Completed && x.Result != BuildResult.Canceled)
                        .ToList();
                    if (builds.Count > 0)
                    {
                        build = builds[0];
                    }
                }
                catch (Exception)
                {
                    // Error enumerating builds, continue to the next one
                }

                if (build is object)
                {
                    yield return (pullRequest, build);
                }
            }
        }
    }
}
