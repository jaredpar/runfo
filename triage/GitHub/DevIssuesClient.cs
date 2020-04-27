using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using DevOps.Util;
using Issue = Octokit.Issue;

/// <summary>
/// Client used during development to map the issues updated to the devops-util repository
/// so we can test without having to update the real repositories
/// </summary>
internal sealed class DevIssuesClient : IIssuesClient
{
    internal GitHubClient GitHubClient { get; }

    internal Dictionary<string, GitHubIssueKey> IssueMap { get; } = new Dictionary<string, GitHubIssueKey>();

    internal DevIssuesClient(GitHubClient gitHubClient)
    {
        GitHubClient = gitHubClient;

        IssueMap[GetKey("dotnet", "runtime", 702)] = new GitHubIssueKey("jaredpar", "devops-util", 5);
        IssueMap[GetKey("dotnet", "runtime", 34472)] = new GitHubIssueKey("jaredpar", "devops-util", 8);
    }

    internal static string GetKey(string organization, string repository, int number) => $"{organization}-{repository}-{number}";


    public IAssigneesClient Assignee => throw new NotImplementedException();

    public IIssuesEventsClient Events => throw new NotImplementedException();

    public IMilestonesClient Milestone => throw new NotImplementedException();

    public IIssuesLabelsClient Labels => throw new NotImplementedException();

    public IIssueCommentsClient Comment => throw new NotImplementedException();

    public IIssueTimelineClient Timeline => throw new NotImplementedException();

    public Task<Issue> Create(string owner, string name, NewIssue newIssue)
    {
        throw new NotImplementedException();
    }

    public Task<Issue> Create(long repositoryId, NewIssue newIssue)
    {
        throw new NotImplementedException();
    }

    public Task<Issue> Get(string owner, string name, int number)
    {
        var key = GetKey(owner, name, number);
        if (IssueMap.TryGetValue(key, out var issueKey))
        {
            owner = issueKey.Organization;
            name = issueKey.Repository;
            number = issueKey.Number;
        }

        return GitHubClient.Issue.Get(owner,name, number);
    }

    public Task<Issue> Get(long repositoryId, int number)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForCurrent()
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForCurrent(ApiOptions options)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForCurrent(IssueRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForCurrent(IssueRequest request, ApiOptions options)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForOrganization(string organization)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForOrganization(string organization, ApiOptions options)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForOrganization(string organization, IssueRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForOrganization(string organization, IssueRequest request, ApiOptions options)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForOwnedAndMemberRepositories()
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForOwnedAndMemberRepositories(ApiOptions options)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForOwnedAndMemberRepositories(IssueRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForOwnedAndMemberRepositories(IssueRequest request, ApiOptions options)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForRepository(string owner, string name)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForRepository(long repositoryId)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForRepository(string owner, string name, ApiOptions options)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForRepository(long repositoryId, ApiOptions options)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForRepository(string owner, string name, RepositoryIssueRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForRepository(long repositoryId, RepositoryIssueRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForRepository(string owner, string name, RepositoryIssueRequest request, ApiOptions options)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Issue>> GetAllForRepository(long repositoryId, RepositoryIssueRequest request, ApiOptions options)
    {
        throw new NotImplementedException();
    }

    public Task Lock(string owner, string name, int number)
    {
        throw new NotImplementedException();
    }

    public Task Lock(long repositoryId, int number)
    {
        throw new NotImplementedException();
    }

    public Task Unlock(string owner, string name, int number)
    {
        throw new NotImplementedException();
    }

    public Task Unlock(long repositoryId, int number)
    {
        throw new NotImplementedException();
    }

    public async Task<Issue> Update(string owner, string name, int number, IssueUpdate issueUpdate)
    {
        var key = GetKey(owner, name, number);
        if (IssueMap.TryGetValue(key, out var issueKey))
        {
            return await GitHubClient.Issue.Update(issueKey.Organization, issueKey.Repository, issueKey.Number, issueUpdate);
        }

        // Don't update items not in the map
        return await GitHubClient.Issue.Get(owner, name, number);
    }

    public Task<Issue> Update(long repositoryId, int number, IssueUpdate issueUpdate)
    {
        throw new NotImplementedException();
    }
}
