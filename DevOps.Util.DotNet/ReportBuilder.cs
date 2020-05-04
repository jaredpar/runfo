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
            IEnumerable<(BuildInfo BuildInfo, string JobName)> results,
            bool markdown,
            bool includeDefinition,
            string? footer = null)
        {
            var builder = new StringBuilder();
            if (markdown)
            {
                builder.AppendLine(MarkdownReportStart);
                if (includeDefinition)
                {
                    builder.Append("|Definition");
                }

                builder.AppendLine("|Build|Kind|Job Name|");

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

                    builder.Append("|");
                    AppendBuildLink(builder, buildInfo);
                    builder.Append("|");
                    AppendBuildKind(builder, buildInfo);
                    builder.AppendLine($"|{result.JobName}|");
                }
                else
                {
                    builder.AppendLine(buildInfo.BuildUri);
                }
            }

            // Need a line break to separate the table from the footer and report end

            builder.AppendLine();
            if (footer is object)
            {
                builder.AppendLine(footer);
            }

            if (markdown)
            {
                builder.AppendLine(MarkdownReportEnd);
            }

            return builder.ToString();
        }

        public string BuildSearchHelix(
            IEnumerable<(BuildInfo BuildInfo, HelixLogInfo HelixLogInfo)> results,
            HelixLogKind[] kinds,
            bool markdown,
            string? footer = null)
        {
            var builder = new StringBuilder();
            if (markdown)
            {
                builder.AppendLine(MarkdownReportStart);
                builder.Append("|Build|Kind|");

                var header = "|---|---|";
                foreach (var kind in kinds)
                {
                    var columnName = GetTitleName(kind);
                    builder.Append($"{columnName}|");
                    header += "---|";
                }
                builder.AppendLine();
                builder.AppendLine(header);

                foreach (var tuple in results)
                {
                    var buildInfo = tuple.BuildInfo;
                    var helixLogInfo = tuple.HelixLogInfo;
                    builder.Append("|");
                    AppendBuildLink(builder, buildInfo);
                    builder.Append("|");
                    AppendBuildKind(builder, buildInfo);
                    builder.Append("|");
                    foreach (var kind in kinds)
                    {
                        var uri = helixLogInfo.GetUri(kind);
                        if (uri is null)
                        {
                            builder.Append("|");
                            continue;
                        }

                        var name = GetValueName(kind);
                        builder.Append($"[{name}]({uri})|");
                    }
                    builder.AppendLine();
                }

                AppendFooter();
                builder.AppendLine(MarkdownReportEnd);
            }
            else
            {
                foreach (var tuple in results)
                {
                    var buildInfo = tuple.BuildInfo;
                    var helixLogInfo = tuple.HelixLogInfo;

                    builder.AppendLine(buildInfo.BuildUri);
                    foreach (var kind in kinds)
                    {
                        var name = GetTitleName(kind);
                        var uri = helixLogInfo.GetUri(kind);
                        builder.AppendLine($"  {name} - {uri}");
                    }
                }
                AppendFooter();
            }

            return builder.ToString();

            void AppendFooter()
            {
                if (footer is object)
                {
                    builder.AppendLine();
                    builder.AppendLine(footer);
                }
            }

            static string GetTitleName(HelixLogKind kind) => kind switch 
            {
                HelixLogKind.Console => "Console",
                HelixLogKind.CoreDump => "Core Dump",
                HelixLogKind.RunClient => "Run Client",
                HelixLogKind.TestResults => "Test Results",
                _ => throw new InvalidOperationException($"Invalid kind {kind}")
            };

            static string GetValueName(HelixLogKind kind) => kind switch
            {
                HelixLogKind.Console => "console.log",
                HelixLogKind.CoreDump => "core dump",
                HelixLogKind.RunClient => "runclient.py",
                HelixLogKind.TestResults => "test results",
                _ => throw new InvalidOperationException($"Invalid kind {kind}"),
            };
        }

        private static void AppendBuildLink(StringBuilder builder, BuildInfo buildInfo)
        {
            builder.Append($"[{buildInfo.Number}]({buildInfo.BuildUri})");
        }

        private static void AppendBuildKind(StringBuilder builder, BuildInfo buildInfo)
        {
            var kind = "Rolling";
            if (buildInfo.PullRequestKey is GitHubPullRequestKey prKey)
            {
                kind = $"[PR {prKey.Number}]({prKey.PullRequestUri})";
            }

            builder.Append(kind);
        }
    }
}