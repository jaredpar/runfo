using DevOps.Util.DotNet;
using Octokit;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.UnitTests
{
    public sealed class TestableGitHubClientFactory : IGitHubClientFactory
    {
        public TestableGitHubClient TestableGitHubClient { get; } = new TestableGitHubClient();

        public Task<IGitHubClient> CreateForAppAsync(string owner, string repository) => Task.FromResult<IGitHubClient>(TestableGitHubClient);
    }

    public sealed class TestableGitHubClient : IGitHubClient
    {
        public IConnection Connection => throw new NotImplementedException();

        public IAuthorizationsClient Authorization => throw new NotImplementedException();

        public IActivitiesClient Activity => throw new NotImplementedException();

        public IGitHubAppsClient GitHubApps => throw new NotImplementedException();

        public IIssuesClient Issue => throw new NotImplementedException();

        public IMigrationClient Migration => throw new NotImplementedException();

        public IMiscellaneousClient Miscellaneous => throw new NotImplementedException();

        public IOauthClient Oauth => throw new NotImplementedException();

        public IOrganizationsClient Organization => throw new NotImplementedException();

        public IPullRequestsClient PullRequest => throw new NotImplementedException();

        public IRepositoriesClient Repository => throw new NotImplementedException();

        public IGistsClient Gist => throw new NotImplementedException();

        public IUsersClient User => throw new NotImplementedException();

        public IGitDatabaseClient Git => throw new NotImplementedException();

        public ISearchClient Search => throw new NotImplementedException();

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
