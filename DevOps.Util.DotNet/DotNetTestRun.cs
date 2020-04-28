using DevOps.Util;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace DevOps.Util.DotNet
{
    public readonly struct DotNetTestRunInfo
    {
        public Build Build { get; }
        public TestRun TestRun { get; }

        internal DotNetTestRunInfo(Build build, TestRun testRun)
        {
            Build = build;
            TestRun = testRun;
        }
    }

    public sealed class DotNetTestRun
    {
        public DotNetTestRunInfo TestRunInfo { get; }
        public ReadOnlyCollection<DotNetTestCaseResult> TestCaseResults { get; }

        public Build Build => TestRunInfo.Build;

        public TestRun TestRun => TestRunInfo.TestRun;

        public DotNetTestRun(DotNetTestRunInfo testRunInfo, ReadOnlyCollection<DotNetTestCaseResult> testCaseResults)
        {
            TestRunInfo = testRunInfo;
            TestCaseResults = testCaseResults;
        }
    }
}