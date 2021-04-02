using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DevOps.Util;

namespace DevOps.Util.DotNet
{
    public sealed class ReportBuilder
    {
        public string BuildSearchTests(
            IEnumerable<(BuildAndDefinitionInfo BuildAndDefinitionInfo, string? TestRunName, HelixLogInfo? LogInfo)> results,
            bool includeDefinition,
            bool includeHelix)
        {
            var builder = new StringBuilder();

            BuildHeader();

            var resultsCount = 0;
            foreach (var result in results)
            {
                resultsCount++;
                var buildAndDefinitionInfo = result.BuildAndDefinitionInfo;

                builder.Append('|');
                AppendBuildLink(builder, buildAndDefinitionInfo.BuildInfo);

                if (includeDefinition)
                {
                    var definitionName = buildAndDefinitionInfo.DefinitionName;
                    var definitionUri = buildAndDefinitionInfo.DefinitionInfo.DefinitionUri;
                    builder.Append($"|[{definitionName}]({definitionUri})");
                }

                builder.Append('|');
                AppendBuildKind(builder, buildAndDefinitionInfo.BuildInfo);
                builder.Append($"|{result.TestRunName}");

                if (includeHelix)
                {
                    var helixLog = result.LogInfo ?? HelixLogInfo.Empty;
                    AppendHelixLog(builder, helixLog, HelixLogKind.Console);
                    AppendHelixLog(builder, helixLog, HelixLogKind.CoreDump);
                    AppendHelixLog(builder, helixLog, HelixLogKind.TestResults);
                    AppendHelixLog(builder, helixLog, HelixLogKind.RunClient);
                }

                builder.AppendLine("|");
            }

            builder.AppendLine();

            return builder.ToString();

            void BuildHeader()
            {
                int columnCount = 1;
                builder.Append("|Build");

                if (includeDefinition)
                {
                    builder.Append("|Definition");
                    columnCount++;
                }

                builder.Append("|Kind|Run Name");
                columnCount += 2;

                if (includeHelix)
                {
                    AppendHelixColumn(HelixLogKind.Console);
                    AppendHelixColumn(HelixLogKind.CoreDump);
                    AppendHelixColumn(HelixLogKind.TestResults);
                    AppendHelixColumn(HelixLogKind.RunClient);
                    columnCount += 4;

                    void AppendHelixColumn(HelixLogKind kind) => builder.Append($"|{GetHelixColumnName(kind)}");
                }

                builder.AppendLine("|");

                for (int i =0 ; i < columnCount; i++)
                {
                    builder.Append("|---");
                }

                builder.AppendLine("|");
            }
        }

        // TODO: remove definition or structure it so that it's an optional parameter here
        public string BuildSearchTimeline(
            IEnumerable<(BuildAndDefinitionInfo BuildAndDefinitionInfo, string? JobName)> results,
            bool markdown,
            bool includeDefinition,
            string? footer = null)
        {
            var builder = new StringBuilder();
            if (markdown)
            {
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
                var buildAndDefinitionInfo = result.BuildAndDefinitionInfo;
                if (markdown)
                {
                    if (includeDefinition)
                    {
                        var definitionName = buildAndDefinitionInfo.DefinitionName;
                        var definitionUri = buildAndDefinitionInfo.DefinitionInfo.DefinitionUri;
                        builder.Append($"|[{definitionName}]({definitionUri})");
                    }

                    builder.Append("|");
                    AppendBuildLink(builder, buildAndDefinitionInfo.BuildInfo);
                    builder.Append("|");
                    AppendBuildKind(builder, buildAndDefinitionInfo.BuildInfo);
                    builder.AppendLine($"|{result.JobName}|");
                }
                else
                {
                    builder.AppendLine(buildAndDefinitionInfo.BuildUri);
                }
            }

            // Need a line break to separate the table from the footer and report end

            builder.AppendLine();
            if (footer is object)
            {
                builder.AppendLine(footer);
            }

            return builder.ToString();
        }

        public string BuildSearchHelix(
            IEnumerable<(BuildInfo BuildInfo, HelixLogInfo? HelixLogInfo)> results,
            HelixLogKind[] kinds,
            bool markdown,
            string? footer = null)
        {
            var builder = new StringBuilder();
            if (markdown)
            {
                builder.Append("|Build|Kind|");

                var header = "|---|---|";
                foreach (var kind in kinds)
                {
                    var columnName = GetHelixColumnName(kind);
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
                        var uri = helixLogInfo?.GetUri(kind);
                        if (uri is null)
                        {
                            builder.Append("|");
                            continue;
                        }

                        var name = GetHelixKindValueName(kind);
                        builder.Append($"[{name}]({uri})|");
                    }
                    builder.AppendLine();
                }

                AppendFooter();
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
                        var name = GetHelixColumnName(kind);
                        var uri = helixLogInfo?.GetUri(kind);
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
        }

        public string BuildManual(IEnumerable<(BuildInfo BuildInfo, DateTime QueueTime)> results)
        {
            var builder = new StringBuilder();
            builder.AppendLine("|Build|Kind|Start Time|");
            builder.AppendLine("|---|---|---|");
            foreach (var tuple in results)
            {
                AppendBuildLink(builder, tuple.BuildInfo);
                builder.Append('|');
                AppendBuildKind(builder, tuple.BuildInfo);
                builder.Append('|');
                builder.Append(tuple.QueueTime.ToString("yyyy-dd-MM"));
                builder.Append('|');
                builder.AppendLine();
            }
            return builder.ToString();
        }

        private static string GetHelixColumnName(HelixLogKind kind) => kind.GetDisplayName();

        private static string GetHelixKindValueName(HelixLogKind kind) => kind.GetDisplayFileName();

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

        private static void AppendHelixLog(StringBuilder builder, HelixLogInfo logInfo, HelixLogKind kind)
        {
            builder.Append('|');

            var uri = logInfo.GetUri(kind);
            if (uri is null)
            {
                return;
            }

            var name = GetHelixKindValueName(kind);
            builder.Append($"[{name}]({uri})");
        }
    }
}