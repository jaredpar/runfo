using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public record BuildInfo(
            string Title,
            string RollingRate,
            SearchBuildsRequest RollingRequest,
            string MergedPullRequestRate,
            SearchBuildsRequest MergedPullRequestRequest,
            string TotalRate,
            SearchBuildsRequest TotalRequest);
        public record DefinitionInfo(ModelBuildDefinition ModelBuildDefinition, DefinitionKey DefinitionKey, BuildInfo[] BuildInfos);

        public TriageContextUtil TriageContextUtil { get; }
        [BindProperty(SupportsGet = true, Name = "id")]
        public int? DefinitionId { get; set; }
        public DefinitionInfo? Definition { get; set; }
        [BindProperty(SupportsGet = true, Name = "targetBranch")]
        public string? TargetBranch { get; set; }

        public DefinitionModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public async Task OnGet()
        {
            if (DefinitionId is { } definitionId)
            {
                TargetBranch ??= "master";
                var modelBuildDefiniton = await TriageContextUtil.Context
                    .ModelBuildDefinitions
                    .Where(x => x.DefinitionId == definitionId)
                    .SingleAsync();

                var now = DateTime.UtcNow;
                var limit = now - TimeSpan.FromDays(21);
                var buildsRequest = new SearchBuildsRequest()
                {
                    Definition = definitionId.ToString(),
                    Started = new DateRequestValue(limit, RelationalKind.GreaterThan),
                    BuildType = new BuildTypeRequestValue(ModelBuildKind.PullRequest, EqualsKind.NotEquals),
                    TargetBranch = new StringRequestValue(TargetBranch, StringRelationalKind.Equals),
                };

                var builds = await buildsRequest
                    .Filter(TriageContextUtil.Context.ModelBuilds)
                    .Select(x => new
                    {
                        x.BuildResult,
                        x.IsMergedPullRequest,
                        x.PullRequestNumber,
                        x.StartTime,
                    })
                    .ToListAsync();

                Definition = new DefinitionInfo(
                    modelBuildDefiniton,
                    modelBuildDefiniton.GetDefinitionKey(),
                    new[]
                    {
                        GetForDate(7),
                        GetForDate(14),
                        GetForDate(21),
                    });

                BuildInfo GetForDate(int days)
                {
                    var limit = now - TimeSpan.FromDays(days);
                    var title = $"{days} days";

                    var filtered = builds.Where(x => x.StartTime >= limit).ToList();
                    double count = filtered.Count;
                    var rolling = filtered.Where(x => x.PullRequestNumber is null).Select(x => x.BuildResult);
                    var mpr = filtered.Where(x => x.IsMergedPullRequest).Select(x => x.BuildResult);
                    var total = filtered.Where(x => x.IsMergedPullRequest || x.PullRequestNumber is null).Select(x => x.BuildResult);

                    return new BuildInfo(
                        title,
                        GetRate(rolling),
                        new SearchBuildsRequest($"definition:{definitionId} started:~{days} kind:rolling targetBranch:{TargetBranch}"),
                        GetRate(mpr),
                        new SearchBuildsRequest($"definition:{definitionId} started:~{days} kind:mpr targetBranch:{TargetBranch}"),
                        GetRate(total),
                        new SearchBuildsRequest($"definition:{definitionId} started:~{days} kind:!pr targetBranch:{TargetBranch}"));
                    string GetRate(IEnumerable<BuildResult?> e)
                    {
                        double totalCount = e.Count();
                        double passedCount = e.Count(x => x is BuildResult.Succeeded or BuildResult.PartiallySucceeded);
                        return (passedCount / totalCount).ToString("P2");
                    }

                }
            }

            /*
            ErrorMessage = null;

            if (string.IsNullOrEmpty(Query))
            {
                Query = new SearchBuildsRequest()
                {
                    Started = new DateRequestValue(dayQuery: 5),
                    BuildType = new BuildTypeRequestValue(ModelBuildKind.PullRequest, EqualsKind.NotEquals, "pr"),
                }.GetQueryString();
            }

            if (string.IsNullOrEmpty(Definition))
            {
                Definition = "roslyn-ci";
                return;
            }

            var modelBuildDefinition = await FindDefinitionAsync(Definition);
            if (modelBuildDefinition is null)
            {
                ErrorMessage = $"Could not find definition for {Definition}";
                return;
            }

            var searchBuildsRequest = new SearchBuildsRequest();
            searchBuildsRequest.ParseQueryString(Query);
            searchBuildsRequest.Definition = null;

            await PopulateBuildInfoAsync();
            await PopulateJobData();

            HasResults = true;

            async Task PopulateBuildInfoAsync()
            {
                var buildQuery = TriageContextUtil.Context.ModelBuilds
                    .Where(x => x.ModelBuildDefinitionId == modelBuildDefinition.Id && x.BuildResult.HasValue);
                var builds = await searchBuildsRequest.Filter(buildQuery)
                    .Select(x => new
                    {
                        x.BuildResult,
                        x.IsMergedPullRequest,
                        x.PullRequestNumber,
                    })
                    .ToListAsync();

                BuildCount = builds.Count;
                RollingPassRate = GetPassRate(ModelBuildKind.Rolling);
                MergedPullRequestPassRate = GetPassRate(ModelBuildKind.MergedPullRequest);

                var totalPassRate = (double)(builds.Count(x => IsSuccess(x.BuildResult!.Value))) / builds.Count;
                TotalPassRate = totalPassRate.ToString("P2");

                string? GetPassRate(ModelBuildKind kind)
                {
                    var filtered = builds
                        .Where(x => TriageContextUtil.GetModelBuildKind(x.IsMergedPullRequest, x.PullRequestNumber) == kind)
                        .ToList();
                    if (filtered.Count == 0)
                    {
                        return null;
                    }

                    var count = filtered.Count(x => IsSuccess(x.BuildResult!.Value));
                    var rate = (double)count / filtered.Count;
                    return rate.ToString("P2");
                }
            }

            async Task PopulateJobData()
            {
                IQueryable<ModelTimelineIssue> query = TriageContextUtil.Context
                    .ModelTimelineIssues
                    .Where(x => x.IssueType == IssueType.Error && x.ModelBuild.ModelBuildDefinitionId == modelBuildDefinition.Id && x.ModelBuild.BuildResult.HasValue);
                query = searchBuildsRequest.Filter(query);
                const int limit = 1_000;
                var issues = await query
                    .Select(x => new
                    {
                        x.JobName,
                        x.ModelBuild.BuildResult,
                        x.ModelBuild.IsMergedPullRequest,
                        x.ModelBuild.PullRequestNumber
                    })
                    .Take(limit)
                    .ToListAsync();
                if (issues.Count == limit)
                {
                    JobLimitHit = limit;
                }

                searchBuildsRequest.Definition = Definition;
                foreach (var group in issues.GroupBy(x => x.JobName))
                {
                    var request = new SearchTimelinesRequest()
                    {
                        JobName = group.Key,
                        Type = IssueType.Error
                    };

                    var data = new JobData()
                    {
                        JobName = group.Key,
                        MergedPullRequestFailureCount = GetFailureCount(ModelBuildKind.MergedPullRequest),
                        RollingFailureCount = GetFailureCount(ModelBuildKind.Rolling),
                        TotalFailureCount = GetFailureCount(null),
                        JobPageBuildQuery = searchBuildsRequest.GetQueryString(),
                        JobPageTimelineQuery = request.GetQueryString(),
                    };

                    JobDataList.Add(data);

                    int GetFailureCount(ModelBuildKind? kind) =>
                        kind is { } k
                            ? group.Where(x => TriageContextUtil.GetModelBuildKind(x.IsMergedPullRequest, x.PullRequestNumber) == k).Count()
                            : group.Count();
                }
                searchBuildsRequest.Definition = null;

                JobDataList.Sort((x, y) => y.TotalFailureCount - x.TotalFailureCount);
            }
            */
        }

        private Task<ModelBuildDefinition?> FindDefinitionAsync(string definition)
        {
            if (int.TryParse(definition, out int id))
            {
                return TriageContextUtil.Context.ModelBuildDefinitions.Where(x => x.DefinitionId == id).FirstOrDefaultAsync();
            }
            else
            {
                return TriageContextUtil.Context.ModelBuildDefinitions.Where(x => x.DefinitionName == definition).FirstOrDefaultAsync();
            }
        }
    }
}
