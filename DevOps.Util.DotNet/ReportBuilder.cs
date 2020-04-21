#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DevOps.Util;
using Mono.Options;

namespace DevOps.Util.DotNet
{
    public sealed class ReportBuilder
    {
        public static readonly string MarkdownReportStart = "<!-- runfo report start -->";
        public static readonly Regex MarkdownReportStartRegex = new Regex(@"<!--\s*runfo report start\s*-->", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly string MarkdownReportEnd = "<!-- runfo report end -->";
        public static readonly Regex MarkdownReportEndRegex = new Regex(@"<!--\s*runfo report end\s*-->", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string BuildSearchTimeline(
            IEnumerable<(Build Build, string TimelineRecordName)> results,
            bool markdown,
            bool includeDefinition)
        {
            var builder = new StringBuilder();
            if (markdown)
            {
                builder.AppendLine(MarkdownReportStart);
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

            var resultsCount = 0;
            foreach (var result in results)
            {
                resultsCount++;
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
                    if (DevOpsUtil.TryGetPullRequestKey(build, out var prKey))
                    {
                        kind = $"PR {prKey.PullRequestUri}";
                    }
                    builder.AppendLine($"|[{build.Id}]({DevOpsUtil.GetBuildUri(build)})|{kind}|{result.TimelineRecordName}|");
                }
                else
                {
                    builder.AppendLine(DevOpsUtil.GetBuildUri(build).ToString());
                }
            }

            var foundBuildCount = results.GroupBy(x => x.Build.Id).Count();
            builder.AppendLine();
            builder.AppendLine($"Impacted {foundBuildCount} builds");
            builder.AppendLine($"Impacted {resultsCount} jobs");

            if (markdown)
            {
                builder.AppendLine(MarkdownReportEnd);
            }

            return builder.ToString();
        }
    }
}