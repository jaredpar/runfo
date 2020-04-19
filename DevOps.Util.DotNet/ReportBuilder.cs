using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DevOps.Util;
using Mono.Options;

namespace DevOps.Util.DotNet
{
    public sealed class ReportBuilder
    {
        public string BuildSearchTimeline(
            IEnumerable<SearchTimelineResult> searchTimelineResults,
            int searchBuildCount,
            bool markdown,
            bool includeDefinition)
        {
            var builder = new StringBuilder();
            if (markdown)
            {
                if (includeDefinition)
                {
                    builder.Append("|Definition");
                }

                builder.AppendLine("|Build|Kind|Timeline Record|");

                if (includeDefinition)
                {
                    builder.Append("|---");
                }

                builder.AppendLine("|---|---|---|");
            }

            foreach (var result in searchTimelineResults)
            {
                var build = result.Build;
                if (markdown)
                {
                    if (includeDefinition)
                    {
                        var definitionName = DotNetUtil.GetDefinitionName(build);
                        var definitionUri = DevOpsUtil.GetBuildDefinitionUri(build);
                        builder.Append($"|[{definitionName}]({definitionUri})");
                    }

                    var kind = "Rolling";
                    if (DevOpsUtil.GetPullRequestNumber(build) is int pr)
                    {
                        kind = $"PR https://github.com/{build.Repository.Id}/pull/{pr}";
                    }
                    builder.AppendLine($"|[{build.Id}]({DevOpsUtil.GetBuildUri(build)})|{kind}|{result.TimelineRecord.Name}|");
                }
                else
                {
                    builder.AppendLine(DevOpsUtil.GetBuildUri(build).ToString());
                }
            }

            var foundBuildCount = searchTimelineResults.GroupBy(x => x.Build.Id).Count();
            builder.AppendLine();
            builder.AppendLine($"Evaluated {searchBuildCount} builds");
            builder.AppendLine($"Impacted {foundBuildCount} builds");
            builder.AppendLine($"Impacted {searchTimelineResults.Count()} jobs");
            return builder.ToString();
        }
    }

}