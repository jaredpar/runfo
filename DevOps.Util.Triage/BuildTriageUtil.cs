
#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DevOps.Util.Triage
{
    internal sealed class BuildTriageUtil
    {
        internal DevOpsServer Server { get; }

        internal DotNetQueryUtil QueryUtil { get; }

        internal TriageContextUtil TriageContextUtil { get; }

        private ILogger Logger { get; }

        internal Build Build { get; }

        internal BuildInfo BuildInfo { get; }

        internal ModelBuild ModelBuild { get; }

        internal Timeline? Timeline { get; set; }

        internal bool HasSetTimeline { get; set; }

        internal List<HelixWorkItem>? HelixWorkItems { get; set; }

        internal List<(HelixWorkItem WorkItem, HelixLogInfo LogInfo)>? HelixLogInfos { get; set; }

        internal Dictionary<string, TimelineRecord> HelixJobToRecordMap { get; set; }

        internal TriageContext Context => TriageContextUtil.Context;

        internal BuildTriageUtil(
            Build build,
            BuildInfo buildInfo,
            ModelBuild modelBuild,
            DevOpsServer server,
            TriageContextUtil triageContextUtil,
            ILogger logger)
        {
            Build = build;
            BuildInfo = buildInfo;
            ModelBuild = modelBuild;
            Server = server;
            TriageContextUtil = triageContextUtil;
            QueryUtil = new DotNetQueryUtil(server);
            Logger = logger;
        }

        internal async Task TriageAsync()
        {
            Logger.LogInformation($"Triaging {DevOpsUtil.GetBuildUri(Build)}");

            var query = 
                from issue in Context.ModelTriageIssues
                from complete in Context.ModelTriageIssueResultCompletes
                    .Where(complete =>
                        complete.ModelTriageIssueId == issue.Id &&
                        complete.ModelBuildId == ModelBuild.Id)
                    .DefaultIfEmpty()
                select new { issue, complete };

            foreach (var data in query.ToList())
            {
                if (data.complete is object)
                {
                    continue;
                }

                var issue = data.issue;
                switch (issue.SearchKind)
                {
                    case SearchKind.SearchTimeline:
                        await DoSearchTimelineAsync(issue);
                        break;
                    case SearchKind.SearchHelixRunClient:
                        await DoSearchHelixRunClientAsync(issue);
                        break;
                    default:
                        Logger.LogWarning($"Unknown search kind {issue.SearchKind} in {issue.Id}");
                        break;
                }
            }
        }

        private async Task DoSearchTimelineAsync(ModelTriageIssue modelTriageIssue)
        {
            await EnsureTimelineAsync().ConfigureAwait(false);
            if (Timeline is object)
            {
                DoSearchTimeline(modelTriageIssue, Timeline);
            }

            async Task EnsureTimelineAsync()
            {
                if (HasSetTimeline)
                {
                    return;
                }

                try
                {
                    Timeline = await Server.GetTimelineAsync(Build);
                    if (Timeline is null)
                    {
                        Logger.LogWarning("No timeline");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error getting timeline: {ex.Message}");
                }

                HasSetTimeline = true;
            }
        }

        private void DoSearchTimeline(ModelTriageIssue modelTriageIssue, Timeline timeline)
        {
            var searchText = modelTriageIssue.SearchText;
            Logger.LogInformation($@"Text: ""{searchText}""");
            if (TriageContextUtil.IsProcessed(modelTriageIssue, ModelBuild))
            {
                Logger.LogInformation($@"Skipping");
                return;
            }

            var count = 0;
            foreach (var result in QueryUtil.SearchTimeline(Build, timeline, text: searchText))
            {
                count++;

                var modelTriageIssueResult = new ModelTriageIssueResult()
                {
                    TimelineRecordName = result.ResultRecord.Name,
                    JobName = result.JobName,
                    Line = result.Line,
                    ModelBuild = ModelBuild,
                    ModelTriageIssue = modelTriageIssue,
                    BuildNumber = result.Build.GetBuildKey().Number,
                };
                Context.ModelTriageIssueResults.Add(modelTriageIssueResult);
            }

            var complete = new ModelTriageIssueResultComplete()
            {
                ModelTriageIssue = modelTriageIssue,
                ModelBuild = ModelBuild,
            };
            Context.ModelTriageIssueResultCompletes.Add(complete);

            try
            {
                Logger.LogInformation($@"Saving {count} jobs");
                Context.SaveChanges();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Cannot save timeline complete: {ex.Message}");
            }
        }

        private async Task DoSearchHelixRunClientAsync(ModelTriageIssue modelTriageIssue)
        {
            await EnsureHelixLogInfosAsync().ConfigureAwait(false);
            Debug.Assert(HelixLogInfos is object);
            await DoSearchHelixRunClientAsync(modelTriageIssue, HelixLogInfos);
        }

        private async Task DoSearchHelixRunClientAsync(
            ModelTriageIssue modelTriageIssue,
            List<(HelixWorkItem WorkItem, HelixLogInfo LogInfo)> helixLogInfos)
        {
            // Guarding against a catostrophic PR failure here. When more than this many work items
            // fail abandon auto-triage
            //
            // The number chosen here is 100% arbitrary. Can adjust as more data comes in.
            if (helixLogInfos.Count > 10)
            {
                return;
            }

            foreach (var tuple in helixLogInfos)
            {
                var workItem = tuple.WorkItem;
                var helixLogInfo = tuple.LogInfo;
                if (helixLogInfo.RunClientUri is null)
                {
                    continue;
                }

                // TODO: should cache the log download so it can be used in multiple searches
                Logger.LogInformation($"Downloading helix log {helixLogInfo.RunClientUri}");
                var log = await DownloadHelixLogAsync(helixLogInfo.RunClientUri).ConfigureAwait(false);
                if (log is object && IsMatch(log, modelTriageIssue.SearchText))
                {
                    // TODO: missing all the timeline record data here
                    var modelTriageIssueResult = new ModelTriageIssueResult()
                    {
                        HelixJobId = workItem.JobId,
                        HelixWorkItem = workItem.WorkItemName,
                        ModelBuild = ModelBuild,
                        ModelTriageIssue = modelTriageIssue,
                        BuildNumber = BuildInfo.Number,
                    };
                    Context.ModelTriageIssueResults.Add(modelTriageIssueResult);
                }
            }

            var complete = new ModelTriageIssueResultComplete()
            {
                ModelTriageIssue = modelTriageIssue,
                ModelBuild = ModelBuild,
            };
            Context.ModelTriageIssueResultCompletes.Add(complete);

            // TODO: should batch all the saves and not do it issue by issue
            try
            {
                Logger.LogInformation($@"Saving helix info");
                Context.SaveChanges();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Cannot save helix complete: {ex.Message}");
            }
        }

        private static bool IsMatch(string log, string text)
        {
            var textRegex = new Regex(text, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            using var reader = new StringReader(log);
            do
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                if (textRegex.IsMatch(line))
                {
                    return true;
                }

            } while (true);

            return false;
        }

        private async Task EnsureHelixWorkItemsAsync()
        {
            if (HelixWorkItems is object)
            {
                return;
            }

            try
            {
                HelixWorkItems = await QueryUtil.ListHelixWorkItemsAsync(
                    Build,
                    DotNetUtil.FailedTestOutcomes);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error getting helix work items: {ex.Message}");
            }

            HelixWorkItems ??= new List<HelixWorkItem>();
        }

        private async Task EnsureHelixLogInfosAsync()
        {
            if (HelixLogInfos is object)
            {
                return;
            }

            await EnsureHelixWorkItemsAsync().ConfigureAwait(false);
            Debug.Assert(HelixWorkItems is object);

            var list = new List<(HelixWorkItem, HelixLogInfo)>();
            foreach (var helixWorkItem in HelixWorkItems)
            {
                try
                {
                    var helixLogInfo = await HelixUtil.GetHelixLogInfoAsync(Server, helixWorkItem).ConfigureAwait(false);
                    list.Add((helixWorkItem, helixLogInfo));
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error getting log info for {helixWorkItem.HelixInfo}: {ex.Message}");
                }
            }

            HelixLogInfos = list;
        }

        private async Task<string?> DownloadHelixLogAsync(string uri) => 
            await Server.HttpClient.DownloadFileAsync(
                uri,
                ex => Logger.LogWarning($"Cannot download helix log {uri}: {ex.Message}"));
    }
}
