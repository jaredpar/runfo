using DevOps.Util;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace DevOps.Util.DotNet
{
    public readonly struct TestRunInfo
    {
        public Build Build { get; }
        public TestRun TestRun { get; }

        internal TestRunInfo(Build build, TestRun testRun)
        {
            Build = build;
            TestRun = testRun;
        }
    }

    public sealed class DotNetTestRun
    {
        public TestRunInfo TestRunInfo { get; }
        public ReadOnlyCollection<DotNetTestCaseResult> TestCaseResults { get; }

        public Build Build => TestRunInfo.Build;

        public TestRun TestRun => TestRunInfo.TestRun;

        public DotNetTestRun(TestRunInfo testRunInfo, ReadOnlyCollection<DotNetTestCaseResult> testCaseResults)
        {
            TestRunInfo = testRunInfo;
            TestCaseResults = testCaseResults;
        }
    }
}