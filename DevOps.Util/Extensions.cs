using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace DevOps.Util
{
    public static class DevOpsUtilExtensions
    {
        #region IEnumerable<T>

        public static IEnumerable<T> SelectNotNull<T>(this IEnumerable<T?> enumerable)
            where T : class
        {
            foreach (var current in enumerable)
            {
                if (current is object)
                {
                    yield return current;
                }
            }
        }

        public static IEnumerable<T> SelectNullableValue<T>(this IEnumerable<T?> enumerable)
            where T : struct
        {
            foreach (var current in enumerable)
            {
                if (current.HasValue)
                {
                    yield return current.Value;
                }
            }
        }

        public static IEnumerable<U> SelectNullableValue<T, U>(this IEnumerable<T> enumerable, Func<T, U?> func)
            where U : struct =>
            enumerable
                .Select(func)
                .SelectNullableValue();

        public static ReadOnlyCollection<T> ToReadOnlyCollection<T>(this IEnumerable<T> enumerable) =>
            new ReadOnlyCollection<T>(enumerable.ToList());

        #endregion

        #region IAsyncEnumerable<T>

        public static async Task<T?> FirstOrDefaultAsync<T>(this IAsyncEnumerable<T> enumerable)
            where T : class
        {
            await foreach (var current in enumerable.ConfigureAwait(false))
            {
                return current;
            }

            return default;
        }

        public static async Task<List<T>> Take<T>(this IAsyncEnumerable<T> enumerable, int count)
        {
            var list = new List<T>();
            if (count == 0)
            {
                return list;
            }

            await foreach (var current in enumerable.ConfigureAwait(false))
            {
                list.Add(current);
                if (list.Count >= count)
                {
                    break;
                }
            }

            return list;
        }

        #endregion

        #region DefinitionReference

        public static DefinitionKey GetDefinitionKey(this DefinitionReference def, string organization) =>
            new DefinitionKey(organization, def.Project.Name, def.Id);

        public static DefinitionInfo GetDefinitionInfo(this DefinitionReference def, string organization) =>
            new DefinitionInfo(GetDefinitionKey(def, organization), def.Name);

        #endregion

        #region Build

        public static BuildKey GetBuildKey(this Build build) => DevOpsUtil.GetBuildKey(build);

        public static BuildInfo GetBuildInfo(this Build build) => DevOpsUtil.GetBuildInfo(build);

        public static BuildAndDefinitionInfo GetBuildAndDefinitionInfo(this Build build) => DevOpsUtil.GetBuildAndDefinitionInfo(build);

        public static DefinitionInfo GetDefinitionInfo(this Build build) => DevOpsUtil.GetDefinitionInfo(build);

        public static BuildResultInfo GetBuildResultInfo(this Build build) => DevOpsUtil.GetBuildResultInfo(build);

        public static DateTimeOffset? GetStartTime(this Build build) => DevOpsUtil.ConvertFromRestTime(build.StartTime);

        public static DateTimeOffset? GetQueueTime(this Build build) => DevOpsUtil.ConvertFromRestTime(build.QueueTime);

        public static DateTimeOffset? GetFinishTime(this Build build) => DevOpsUtil.ConvertFromRestTime(build.FinishTime);

        public static string? GetTargetBranch(this Build build) => DevOpsUtil.GetTargetBranch(build);

        #endregion

        #region BuildInfo

        public static BuildKey GetBuildKey(this BuildResultInfo buildInfo) => DevOpsUtil.GetBuildKey(buildInfo);

        #endregion

        #region BuildArtifact

        public static int? GetByteSize(this BuildArtifact buildArtifact) => DevOpsUtil.GetArtifactByteSize(buildArtifact);

        public static BuildArtifactKind GetKind(this BuildArtifact buildArtifact) => DevOpsUtil.GetArtifactKind(buildArtifact);

        #endregion

        #region Timeline

        public static int GetAttempt(this Timeline timeline) => timeline.Records.Max(x => x.Attempt);


        public static void DumpRecordTree(this Timeline timeline, string filePath)
        {
            using var streamWriter = new StreamWriter(filePath, append: false);
            DumpRecordTree(timeline, streamWriter);
        }

        public static void DumpRecordTree(this Timeline timeline, TextWriter textWriter)
        {
            var any = false;
            foreach (var record in timeline.Records.Where(x => string.IsNullOrEmpty(x.ParentId)))
            {
                any = true;
                DumpNode(record, "");
                DumpLevel(record.Id, "  ");
            }

            // If this is a patch timeline there won't be any roots
            if (!any)
            {
                foreach (var record in timeline.Records)
                {
                    DumpNode(record, "");
                }
            }

            void DumpLevel(string id, string indent)
            {
                foreach (var record in timeline.Records.Where(x => x.ParentId == id))
                {
                    DumpNode(record, indent);
                    DumpLevel(record.Id, indent + "  ");
                }
            }

            void DumpNode(TimelineRecord record, string indent)
            {
                var previousAttempt = "";
                if (record.PreviousAttempts?.Length > 0)
                {
                    foreach (var p in record.PreviousAttempts)
                    {
                        if (previousAttempt == "")
                        {
                            previousAttempt = $" ({p.Attempt}";
                        }
                        else
                        {
                            previousAttempt += $", {p.Attempt}";
                        }
                    }

                    previousAttempt += ")";
                }

                textWriter.WriteLine($"{indent}{record.Attempt}.{previousAttempt} {record.Type} {record.Name} {record.Id}");
            } 
        }

        #endregion

        #region TimelineRecord

        public static bool IsAnySuccess(this TimelineRecord record) =>
            record.Result == TaskResult.Succeeded ||
            record.Result == TaskResult.SucceededWithIssues;

        public static bool IsAnyFailed(this TimelineRecord record) =>
            record.Result == TaskResult.Failed ||
            record.Result == TaskResult.Abandoned ||
            record.Result == TaskResult.Canceled;

        public static DateTimeOffset? GetStartTime(this TimelineRecord record) => DevOpsUtil.ConvertFromRestTime(record.StartTime);

        public static DateTimeOffset? GetFinishTime(this TimelineRecord record) => DevOpsUtil.ConvertFromRestTime(record.FinishTime);

        #endregion

        #region DevOpsServer

        /// <summary>
        /// Get the timeline from the specified attempt
        /// </summary>
        public static async Task<Timeline?> GetTimelineAttemptAsync(this DevOpsServer server, string project, int buildNumber, int attempt)
        {
            var latestTimeline = await server.GetTimelineAsync(project, buildNumber).ConfigureAwait(false);
            if (latestTimeline is null)
            {
                return null;
            }

            // latestTimeline.DumpRecordTree(@"p:\temp\timeline\tree.txt");
            if (latestTimeline.Records.All(x => x.Attempt == attempt))
            {
                return latestTimeline;
            }

            // Calculate the set of timeline IDs that we need to query for
            var previousTimelineIdList = latestTimeline
                .Records
                .Select(x => x.PreviousAttempts?.FirstOrDefault(x => x.Attempt == attempt))
                .Select(x => x?.TimelineId)
                .SelectNotNull()
                .Distinct()
                .ToList();

            var records = TrimLaterAttempts();
            foreach (var previousTimelineId in previousTimelineIdList)
            {
                var previousTimeline = await server.GetTimelineAsync(project, buildNumber, previousTimelineId).ConfigureAwait(false);
                if (previousTimeline is null)
                {
                    continue;
                }

                // previousTimeline.DumpRecordTree(@$"p:\temp\timeline\tree-{previousTimelineId}.txt");
                if (IsFullTimeline(previousTimeline))
                {
                    if (previousTimelineIdList.Count == 1)
                    {
                        return previousTimeline;
                    }

                    throw new Exception("Multiple previous timelines with at least one full");
                }

                records.AddRange(previousTimeline.Records);
            }

            latestTimeline.Records = records.ToArray();
            // latestTimeline.DumpRecordTree(@"p:\temp\timeline\tree-final.txt");
            return latestTimeline;

            // The timelines return by the GetTimelineAttempt method can either be full timelines or patch 
            // onse. If they are full then they will have root nodes
            bool IsFullTimeline(Timeline timeline) => timeline.Records.Any(x => x.ParentId is null);

            // This method will trim out the TimelineRecord entries which are greater than the attempt we are searching
            // for.
            List<TimelineRecord> TrimLaterAttempts()
            {
                var set = latestTimeline.Records.ToHashSet();
                var tree = TimelineTree.Create(latestTimeline);
                foreach (var jobNode in tree.JobNodes)
                {
                    if (jobNode.TimelineRecord.Attempt > attempt)
                    {
                        set.Remove(jobNode.TimelineRecord);
                        foreach (var child in jobNode.GetChildrenRecursive())
                        {
                            set.Remove(child.TimelineRecord);
                        }
                    }
                }

                return set.ToList();
            }
        }

        public static async Task<List<Timeline>> GetTimelineAttemptsAsync(this DevOpsServer server, string project, int buildNumber)
        {
            var timeline = await server.GetTimelineAsync(project, buildNumber).ConfigureAwait(false);
            if (timeline is null)
            {
                return new List<Timeline>();
            }

            var list = new List<Timeline>();
            list.Add(timeline);
            var attempt = timeline.GetAttempt();
            if (attempt == 1)
            {
                return list;
            }

            do
            {
                --attempt;
                timeline = await server.GetTimelineAttemptAsync(project, buildNumber, attempt).ConfigureAwait(false);
                if (timeline is object)
                {
                    list.Add(timeline);
                }

            } while (attempt > 1);

            return list;
        }

        public static Task<string> GetYamlAsync(this DevOpsServer server, string project, int buildNumber) =>
            server.GetBuildLogAsync(project, buildNumber, logId: 1);

        /// <summary>
        /// List the builds for the given pull request
        /// </summary>
        /// <remarks>
        /// The request can filter on the definitions that it built against
        /// 
        /// If the repositoryId REST argument is provided it must be accompanied by repositoryType
        /// </remarks>
        public static Task<List<Build>> ListPullRequestBuildsAsync(
            this DevOpsServer server,
            in GitHubPullRequestKey prKey,
            string project,
            int[]? definitions = null)
        {
            var branchName = $"refs/pull/{prKey.Number}/merge";
            var repositoryInfo = new RepositoryInfo(prKey);
            return server.ListBuildsAsync(
                project,
                definitions: definitions,
                branchName: branchName,
                repositoryId: repositoryInfo.Id,
                repositoryType: repositoryInfo.Type);
        }

        #endregion
    }
}