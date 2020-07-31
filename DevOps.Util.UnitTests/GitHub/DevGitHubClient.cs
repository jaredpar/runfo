using System;
using Octokit;

namespace DevOps.Util.UnitTests
{

    /// <summary>
    /// Client used during development to map the issues updated to the devops-util repository
    /// so we can test without having to update the real repositories
    /// </summary>
    internal sealed class DevGitHubClient : IGitHubClient
    {
        public GitHubClient GitHubClient { get; }

        public DevGitHubClient(GitHubClient gitHubClient)
        {
            GitHubClient = gitHubClient;
        }

        public IConnection Connection => throw new NotImplementedException();

        public IAuthorizationsClient Authorization => throw new NotImplementedException();

        public IActivitiesClient Activity => throw new NotImplementedException();

        public IGitHubAppsClient GitHubApps => throw new NotImplementedException();

        public IIssuesClient Issue => new DevIssuesClient(GitHubClient);

        public IMigrationClient Migration => throw new NotImplementedException();

        public IMiscellaneousClient Miscellaneous => throw new NotImplementedException();

        public IOauthClient Oauth => throw new NotImplementedException();

        public IOrganizationsClient Organization => throw new NotImplementedException();

        public IPullRequestsClient PullRequest => throw new NotImplementedException();

        public IRepositoriesClient Repository => throw new NotImplementedException();

        public IGistsClient Gist => throw new NotImplementedException();

        public IUsersClient User => throw new NotImplementedException();

        public IGitDatabaseClient Git => throw new NotImplementedException();

        public ISearchClient Search => GitHubClient.Search;

        public IEnterpriseClient Enterprise => throw new NotImplementedException();

        public IReactionsClient Reaction => throw new NotImplementedException();

        public IChecksClient Check => throw new NotImplementedException();

        public ApiInfo GetLastApiInfo()
        {
            throw new NotImplementedException();
        }

        public void SetRequestTimeout(TimeSpan timeout)
        {
            throw new NotImplementedException();
        }
    }
}