using System;
using System.Collections.Generic;

namespace DevOps.Util.DotNet
{
    public enum HelixLogKind
    {
        RunClient,

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
            RunClientUri = HelixUtil.RewriteUri(runClientUri);
            ConsoleUri = HelixUtil.RewriteUri(consoleUri);
            CoreDumpUri = HelixUtil.RewriteUri(coreDumpUri);
            TestResultsUri = HelixUtil.RewriteUri(testResultsUri);
        }

        public HelixLogInfo(
            HelixLogKind kind,
            string? uri)
        {
            switch (kind)
            {
                case HelixLogKind.RunClient:
                    RunClientUri = uri;
                    break;
                case HelixLogKind.Console:
                    ConsoleUri = uri;
                    break;
                case HelixLogKind.CoreDump:
                    CoreDumpUri = uri;
                    break;
                case HelixLogKind.TestResults:
                    TestResultsUri = uri;
                    break;
                default:
                    throw new Exception($"Invalid value {kind}");
            }
        }

        public IEnumerable<(HelixLogKind kind, string Uri)> GetUris()
        {
            foreach (object? value in Enum.GetValues(typeof(HelixLogKind)))
            {
                if (value is HelixLogKind kind)
                {
                    var uri = GetUri(kind);
                    if (uri is object)
                    {
                        yield return(kind, uri);
                    }
                }
            }
        }

        public string? GetUri(HelixLogKind kind) => kind switch
        {
            HelixLogKind.RunClient => RunClientUri,
            HelixLogKind.Console => ConsoleUri,
            HelixLogKind.CoreDump => CoreDumpUri,
            HelixLogKind.TestResults => TestResultsUri,
            _ => throw new InvalidOperationException($"Invalid enum value {kind}")
        };

        public HelixLogInfo SetUri(HelixLogKind kind, string? uri)
        {
            var runClientUri = RunClientUri;
            var consoleUri = ConsoleUri;
            var coreDumpUri = CoreDumpUri;
            var testResultsUri = TestResultsUri;

            switch (kind)
            {
                case HelixLogKind.Console:
                    consoleUri = uri;
                    break;
                case HelixLogKind.CoreDump:
                    coreDumpUri = uri;
                    break;
                case HelixLogKind.RunClient:
                    runClientUri = uri;
                    break;
                case HelixLogKind.TestResults:
                    testResultsUri = uri;
                    break;
                default:
                    throw new Exception($"Invalid kind '{kind}'");
            }

            return new HelixLogInfo(
                runClientUri: runClientUri,
                consoleUri: consoleUri,
                coreDumpUri: coreDumpUri,
                testResultsUri: testResultsUri);
        }
    }

}