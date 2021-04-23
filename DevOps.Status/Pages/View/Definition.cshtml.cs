using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Math.EC.Rfc7748;

namespace DevOps.Status.Pages.View
{
    public class DefinitionModel : PageModel
    {
        public record BuildResultInfo(
            string Title,
            string RollingRate,
            SearchBuildsRequest RollingRequest,
            string MergedPullRequestRate,
            SearchBuildsRequest MergedPullRequestRequest,
            string MergedPullRequestInitialRate,
            string TotalRate,
            SearchBuildsRequest TotalRequest);
        public record DefinitionDisplayInfo(ModelBuildDefinition ModelBuildDefinition, DefinitionKey DefinitionKey, BuildResultInfo[] BuildResultInfo);
        public record BuildDisplayInfo(int Number, string Kind);
        public record IssueInfo(GitHubIssueKey IssueKey, List<BuildDisplayInfo> BuildNumbers);

        public TriageContextUtil TriageContextUtil { get; }

        // Data for a given definition
        [BindProperty(SupportsGet = true, Name = "id")]
        public int? DefinitionId { get; set; }
        public DefinitionDisplayInfo? Definition { get; set; }
        [BindProperty(SupportsGet = true, Name = "targetBranch")]
        public string? TargetBranch { get; set; }
        public List<IssueInfo> IssueInfos { get; set; } = new List<IssueInfo>();

        // Data for definition list
        [BindProperty(SupportsGet = true)]
        public string? SearchDefinitionName { get; set; }
        public List<DefinitionInfo> DefinitionInfos { get; set; } = new List<DefinitionInfo>();
        public int DefinitionInfosTotalCount { get; set; }
        public PaginationDisplay? PaginationDisplayDefinitionKeys { get; set; }
        [BindProperty(SupportsGet = true, Name = "pageNumber")]
        public int PageNumber { get; set; }

        public DefinitionModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public Task OnGet() => DefinitionId is { } definitionId
            ? OnGetDefinition(definitionId)
            : OnGetSearch(SearchDefinitionName ?? "");

        private async Task OnGetDefinition(int definitionNumber)
        {
            TargetBranch ??= "main";
            var modelBuildDefiniton = await TriageContextUtil.Context
                .ModelBuildDefinitions
                .Where(x => x.DefinitionNumber == definitionNumber)
                .SingleAsync();
            var now = DateTime.UtcNow;

            await GenerateBuildPassInfo();
            await GenerateIssueInfoList();

            async Task GenerateBuildPassInfo()
            {
                var limit = now - TimeSpan.FromDays(21);
                var started = new DateRequestValue(limit, RelationalKind.GreaterThan);
                var attempts = await TriageContextUtil.Context
                    .ModelBuildAttempts
                    .Include(x => x.ModelBuild)
                    .Where(x =>
                        x.DefinitionNumber == definitionNumber &&
                        x.StartTime >= started.DateTime &&
                        x.BuildKind != ModelBuildKind.PullRequest &&
                        x.GitHubTargetBranch == TargetBranch)
                    .Select(x => new
                    {
                        AttemptNumber = x.Attempt,
                        AttemptResult = x.BuildResult,
                        BuildResult = x.ModelBuild.BuildResult,
                        BuildKind = x.ModelBuild.BuildKind,
                        BuildNumber = x.ModelBuild.BuildNumber,
                        BuildStartTime = x.StartTime,
                        PullRequestNumber = x.ModelBuild.PullRequestNumber,
                    })
                    .ToListAsync();

                var builds = attempts
                    .GroupBy(x => x.BuildNumber)
                    .Select(x => x.First())
                    .Select(x => new
                    {
                        x.BuildNumber,
                        x.BuildResult,
                        x.BuildKind,
                        x.PullRequestNumber,
                        StartTime = x.BuildStartTime
                    })
                    .ToList();

                Definition = new DefinitionDisplayInfo(
                    modelBuildDefiniton,
                    modelBuildDefiniton.GetDefinitionKey(),
                    new[]
                    {
                        GetForDate(7),
                        GetForDate(14),
                        GetForDate(21),
                    });

                BuildResultInfo GetForDate(int days)
                {
                    var limit = now - TimeSpan.FromDays(days);
                    var title = $"{days} days";

                    var filtered = builds.Where(x => x.StartTime >= limit).ToList();
                    double count = filtered.Count;
                    var rolling = filtered.Where(x => x.PullRequestNumber is null).Select(x => x.BuildResult);
                    var mpr = filtered.Where(x => x.BuildKind == ModelBuildKind.MergedPullRequest).Select(x => x.BuildResult);
                    var total = filtered.Select(x => x.BuildResult);

                    var attemptMpr = attempts.Where(x => x.BuildStartTime >= limit && x.AttemptNumber == 1 && x.BuildKind == ModelBuildKind.MergedPullRequest).Select(x => x.AttemptResult).ToList();

                    return new BuildResultInfo(
                        title,
                        GetRate(rolling),
                        new SearchBuildsRequest($"definition:{definitionNumber} started:~{days} kind:rolling targetBranch:{TargetBranch}"),
                        GetRate(mpr),
                        new SearchBuildsRequest($"definition:{definitionNumber} started:~{days} kind:mpr targetBranch:{TargetBranch}"),
                        GetRate(attemptMpr),
                        GetRate(total),
                        new SearchBuildsRequest($"definition:{definitionNumber} started:~{days} kind:!pr targetBranch:{TargetBranch}"));

                    string GetRate(IEnumerable<ModelBuildResult> e)
                    {
                        double totalCount = e.Count();
                        double passedCount = e.Count(x => x is ModelBuildResult.Succeeded or ModelBuildResult.PartiallySucceeded);
                        return (passedCount / totalCount).ToString("P2");
                    }
                }
            }

            async Task GenerateIssueInfoList()
            {
                var buildsRequest = new SearchBuildsRequest()
                {
                    Definition = definitionNumber.ToString(),
                    Started = new DateRequestValue(now - TimeSpan.FromDays(21), RelationalKind.GreaterThan),
                    TargetBranch = new StringRequestValue(TargetBranch, StringRelationalKind.Equals),
                };

                var results = await buildsRequest
                    .Filter(TriageContextUtil.Context.ModelBuilds)
                    .SelectMany(x => x.ModelGitHubIssues)
                    .Select(x => new
                    {
                        x.ModelBuild.BuildNumber,
                        x.ModelBuild.PullRequestNumber,
                        IsMergedPullRequest = x.ModelBuild.BuildKind == ModelBuildKind.MergedPullRequest,
                        GitHubOrganization = x.Organization,
                        GitHubRepository = x.Repository,
                        GitHubIssueNumber = x.Number
                    })
                    .ToListAsync();
                foreach (var group in results.GroupBy(x => new GitHubIssueKey(x.GitHubOrganization, x.GitHubRepository, x.GitHubIssueNumber)))
                {
                    IssueInfos.Add(new IssueInfo(
                        group.Key,
                        group.Select(x => new BuildDisplayInfo(x.BuildNumber, GetKind(x.PullRequestNumber, x.IsMergedPullRequest))).OrderByDescending(x => x.Number).ToList()));

                    static string GetKind(int? prNumber, bool isMergedPullRequest) => (prNumber, isMergedPullRequest) switch
                    {
                        (_, true) => "Merged Pull Request",
                        ({ }, false) => "Pull Request",
                        _ => "Rolling",
                    };
                }

                IssueInfos.Sort((x, y) => -(x.BuildNumbers.Count.CompareTo(y.BuildNumbers.Count)));
            }
        }

        private async Task OnGetSearch(string definitionName)
        {
            int pageSize = 25;
            IQueryable<ModelBuildDefinition> query;
            if (string.IsNullOrEmpty(definitionName))
            {
                query = TriageContextUtil
                    .Context
                    .ModelBuildDefinitions;
            }
            else
            {
                query = TriageContextUtil
                    .Context
                    .ModelBuildDefinitions
                    .Where(x => x.DefinitionName.Contains(definitionName));
            }

            DefinitionInfosTotalCount = await query.CountAsync();
            var results = await query
                .OrderBy(x => x.DefinitionName)
                .Select(x => new
                {
                    x.DefinitionName,
                    x.DefinitionNumber,
                    x.AzureOrganization,
                    x.AzureProject
                })
                .Skip(PageNumber * pageSize)
                .Take(pageSize)
                .ToListAsync();

            foreach (var result in results)
            {
                DefinitionInfos.Add(new DefinitionInfo(
                    result.AzureOrganization,
                    result.AzureProject,
                    result.DefinitionNumber,
                    result.DefinitionName));
            }

            PaginationDisplayDefinitionKeys = new PaginationDisplay(
                "/View/Definition",
                new Dictionary<string, string>()
                {
                    { nameof(SearchDefinitionName), definitionName },
                },
                PageNumber,
                DefinitionInfosTotalCount / pageSize);
        }
    }
}
