using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Math.EC.Rfc7748;

namespace DevOps.Status.Pages.View
{
    public class DefinitionModel : PageModel
    {
        public class JobData
        {
            public string? JobName { get; set; }
            public int RollingFailureCount { get; set; }
            public int MergedPullRequestFailureCount { get; set; }
            public int TotalFailureCount { get; set; }
            public string? JobPageBuildQuery { get; set; }
            public string? JobPageTimelineQuery { get; set; }
        }

        public TriageContextUtil TriageContextUtil { get; }
        [BindProperty(SupportsGet = true)]
        public string? Definition { get; set; }
        [BindProperty(SupportsGet = true, Name = "q")]
        public string? Query { get; set; }
        public string? ErrorMessage { get; set; }
        public bool HasResults { get; set; }
        public bool IncludeMergedPullRequestData { get; set; }
        public int BuildCount { get; set; }
        public string? RollingPassRate { get; set; }
        public string? MergedPullRequestPassRate { get; set; }
        public string? TotalPassRate { get; set; }
        public int? JobLimitHit { get; set; }
        public List<JobData> JobDataList { get; set; } = new List<JobData>();

        public DefinitionModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public async Task OnGet()
        {
            ErrorMessage = null;
            if (string.IsNullOrEmpty(Query))
            {
                Query = new SearchBuildsRequest()
                {
                    Started = new DateRequest(dayQuery: 5)
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
                var builds = await searchBuildsRequest.FilterBuilds(buildQuery)
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
                query = searchBuildsRequest.FilterBuilds(query);
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

            static bool IsSuccess(BuildResult result) => result == BuildResult.Succeeded || result == BuildResult.PartiallySucceeded;
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
