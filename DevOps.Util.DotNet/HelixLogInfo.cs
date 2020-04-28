#nullable enable

using System;

namespace DevOps.Util.DotNet
{
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

    }

}