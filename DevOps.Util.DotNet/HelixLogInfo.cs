#nullable enable

using System;

namespace DevOps.Util.DotNet
{
    public enum HelixLogKind
    {
        RunClientUri,

        Console,

        CoreDump,

        TestResults
    }

    public sealed class HelixLogInfo
    {
        public static readonly HelixLogInfo Empty = new HelixLogInfo(null, null, null, null);

        public string? RunClientUri { get; }

        public string? ConsoleUri { get; }

        public string? CoreDumpUri { get; }

        public string? TestResultsUri { get; }

        public HelixLogInfo(
            string? runClientUri,
            string? consoleUri,
            string? coreDumpUri,
            string? testResultsUri)
        {
            RunClientUri = runClientUri;
            ConsoleUri = consoleUri;
            CoreDumpUri = coreDumpUri;
            TestResultsUri = testResultsUri;
        }

        public string? GetUri(HelixLogKind kind) => kind switch
        {
            HelixLogKind.RunClientUri => RunClientUri,
            HelixLogKind.Console => ConsoleUri,
            HelixLogKind.CoreDump => CoreDumpUri,
            HelixLogKind.TestResults => TestResultsUri,
            _ => throw new InvalidOperationException($"Invalid enum value {kind}")
        };
    }

}