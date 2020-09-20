using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevOps.Status.Pages.Tracking
{
    public class NewTrackingIssueModel : PageModel
    {
        public TriageContext TriageContext { get; }
        public TriageContextUtil TriageContextUtil { get; }
        public StatusGitHubClientFactory GitHubClientFactory { get; }
        public DotNetQueryUtilFactory QueryUtilFactory { get; }
        public ILogger Logger { get; }

        [BindProperty(SupportsGet = true)]
        public TrackingKind TrackingKind { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? SearchText { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? Definition { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? GitHubOrganization { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? GitHubRepository { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? ReportBuildsQuery { get; set; }
        public string? ReportText { get; set; }
        public bool ReportPreviewAvailable { get; set; }

        public string? ErrorMessage { get; set; }

        public NewTrackingIssueModel(TriageContext triageContext, DotNetQueryUtilFactory queryUtilFactory, StatusGitHubClientFactory gitHubClientFactory, ILogger<NewTrackingIssueModel> logger)
        {
            TriageContext = triageContext;
            TriageContextUtil = new TriageContextUtil(triageContext);
            GitHubClientFactory = gitHubClientFactory;
            QueryUtilFactory = queryUtilFactory;
            Logger = logger;
        }

        public async Task<IActionResult> OnGet()
        {
            var generateReport = true;

            if (string.IsNullOrEmpty(Definition))
            {
                Definition = "roslyn-ci";
                generateReport = false;
            }

            if (TrackingKind == TrackingKind.Unknown)
            {
                TrackingKind = TrackingKind.Timeline;
                generateReport = false;
            }

            if (string.IsNullOrEmpty(SearchText))
            {
                SearchText = "Error";
                generateReport = false;
            }

            if (string.IsNullOrEmpty(ReportBuildsQuery))
            {
                ReportBuildsQuery = new SearchBuildsRequest()
                {
                    Started = new DateRequestValue(dayQuery: 5),
                }.GetQueryString();
            }

            if (generateReport)
            {
                await PreviewResultsAsync();
            }

            return Page();
        }

        public async Task<IActionResult> OnPost(string command)
        {
            var isCreate = command != "preview";
            if (TrackingKind == TrackingKind.Unknown)
            {
                ErrorMessage = "Invalid Tracking Kind";
                return Page();
            }

            if (string.IsNullOrEmpty(SearchText))
            {
                ErrorMessage = "Must provide search text";
                return Page();
            }

            ModelBuildDefinition? modelBuildDefinition = null;
            if (!string.IsNullOrEmpty(Definition))
            {
                modelBuildDefinition = await TriageContextUtil.FindModelBuildDefinitionAsync(Definition);
                if (modelBuildDefinition is null)
                {
                    ErrorMessage = $"Cannot find build definition with name or ID: {Definition}";
                    return Page();
                }
            }

            if (isCreate && (string.IsNullOrEmpty(GitHubRepository) || string.IsNullOrEmpty(GitHubOrganization)))
            {
                ErrorMessage = "Must provide GitHub organization and repository";
                return Page();
            }

            if (isCreate)
            {
                /*
                IGitHubClient? gitHubClient = null;
                try
                {
                    gitHubClient = await GitHubClientFactory.CreateForAppAsync(GitHubOrganization, GitHubRepository);
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Cannot create GitHub client for that repository: {ex.Message}";
                    return Page();
                }
                */

                var modelTrackingIssue = await CreateTrackingIssue(null);
                return RedirectToPage(
                    "./Issue",
                    new { id = modelTrackingIssue.Id });
            }
            else
            {
                await PreviewResultsAsync();
                return Page();
            }

            async Task<ModelTrackingIssue> CreateTrackingIssue(IGitHubClient gitHubClient)
            {
                var modelTrackingIssue = new ModelTrackingIssue()
                {
                    IsActive = true,
                    TrackingKind = TrackingKind,
                    SearchQuery = SearchText,
                    ModelBuildDefinition = modelBuildDefinition,
                };
                TriageContext.ModelTrackingIssues.Add(modelTrackingIssue);
                await TriageContext.SaveChangesAsync();

                await InitialTriageAsync(modelTrackingIssue);

                return modelTrackingIssue;
            }

            async Task InitialTriageAsync(ModelTrackingIssue modelTrackingIssue)
            {
                IQueryable<ModelBuildAttempt> buildAttemptQuery = TriageContext.ModelBuildAttempts;
                int days = 1;
                if (modelBuildDefinition is object)
                {
                    days = 3;
                    buildAttemptQuery = buildAttemptQuery.Where(x => x.ModelBuild.ModelBuildDefinitionId == modelBuildDefinition.Id);
                }

                var date = DateTime.UtcNow - TimeSpan.FromDays(days);
                buildAttemptQuery = buildAttemptQuery.Where(x => x.ModelBuild.StartTime >= date);

                var started = DateTime.UtcNow;
                try
                {
                    var attempts = await buildAttemptQuery
                        .Include(x => x.ModelBuild)
                        .ThenInclude(x => x.ModelBuildDefinition)
                        .ToListAsync();
                    var queryUtil = QueryUtilFactory.CreateDotNetQueryUtilForApp();
                    foreach (var attempt in attempts)
                    {
                        var util = new TrackingIssueUtil(queryUtil, TriageContextUtil, Logger);
                        await util.TriageAsync(attempt, modelTrackingIssue);

                        if (DateTime.UtcNow - started > TimeSpan.FromSeconds(20))
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error triaging new issues {ex.Message}");
                }
            }
        }

        private async Task PreviewResultsAsync()
        {
            Debug.Assert(ReportBuildsQuery is object);
            ReportText = await GetReportText();
            ReportPreviewAvailable = true;
        }

        private async Task<string> GetReportText()
        {
            Debug.Assert(ReportBuildsQuery is object);
            var searchBuildsRequests = new SearchBuildsRequest();
            searchBuildsRequests.ParseQueryString(ReportBuildsQuery);

            var reportBuilder = new ReportBuilder();

            switch (TrackingKind)
            {
                case TrackingKind.Timeline:
                    return await GetTimelineReportText();
                default:
                    return "Preview not supported";
            }

            async Task<string> GetTimelineReportText()
            {
                var searchTimelinesRequest = new SearchTimelinesRequest()
                {
                    Text = SearchText,
                };

                IQueryable<ModelTimelineIssue> query = TriageContext.ModelTimelineIssues;
                query = searchBuildsRequests.FilterBuilds(query);
                query = searchTimelinesRequest.FilterTimelines(query);
                query = query
                    .Include(x => x.ModelBuild.ModelBuildDefinition)
                    .OrderByDescending(x => x.ModelBuild.BuildNumber)
                    .Take(100);
                var results = await query.ToListAsync();
                var items = results.Select(x => (x.ModelBuild.GetBuildAndDefinitionInfo(), (string?)x.JobName));
                return reportBuilder.BuildSearchTimeline(
                    items,
                    markdown: true,
                    includeDefinition: true);
            }
        }
    }
}
