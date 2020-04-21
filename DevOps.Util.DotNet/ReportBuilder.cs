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
            IEnumerable<(BuildInfo BuildInfo, string TimelineRecordName)> results,
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
                var buildInfo = result.BuildInfo;
                if (markdown)
                {
                    if (includeDefinition)
                    {
                        var definitionName = buildInfo.DefinitionName;
                        var definitionUri = buildInfo.DefinitionInfo.DefinitionUri;
                        builder.Append($"|[{definitionName}]({definitionUri})");
                    }

                    var kind = "Rolling";
                    if (buildInfo.PullRequestKey.HasValue)
                    {
                        kind = $"PR {buildInfo.PullRequestKey.Value.PullRequestUri}";
                    }
                    builder.AppendLine($"|[{buildInfo.Number}]({buildInfo.BuildUri})|{kind}|{result.TimelineRecordName}|");
                }
                else
                {
                    builder.AppendLine(buildInfo.BuildUri);
                }
            }

            var foundBuildCount = results.GroupBy(x => x.BuildInfo.Number).Count();
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