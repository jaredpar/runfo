using System;

namespace DevOps.Util.DotNet
{
    public sealed class HelixLogInfo
    {
        public static readonly HelixLogInfo Empty = new HelixLogInfo(null, null, null);

        public string ConsoleUri { get; }

        public string CoreDumpUri { get; }

        public string TestResultsUri { get; }

        public HelixLogInfo(
            string consoleUri,
            string coreDumpUri,
            string testResultsUri)
        {
            ConsoleUri = consoleUri;
            CoreDumpUri = coreDumpUri;
            TestResultsUri = testResultsUri;
        }

    }

}