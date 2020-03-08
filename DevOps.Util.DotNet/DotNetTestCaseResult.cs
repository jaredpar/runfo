using DevOps.Util;
using System.Diagnostics;

namespace DevOps.Util.DotNet
{
    public sealed class DotNetTestCaseResult
    {
        internal TestRunInfo TestRunInfo { get; }

        // The TestCaseResult representing the actual test failure
        public TestCaseResult TestCaseResult { get; }

        // Contains all the Helix info when this is a Helix test case result
        public HelixWorkItem? HelixWorkItem { get; }

        public HelixInfo? HelixInfo => HelixWorkItem?.HelixInfo;

        public bool IsHelixTestResult => HelixWorkItem.HasValue;

        public bool IsHelixWorkItem => HelixUtil.IsHelixWorkItem(TestCaseResult);

        public string TestCaseTitle => TestCaseResult.TestCaseTitle;

        public TestRun TestRun => TestRunInfo.TestRun;

        public Build Build => TestRunInfo.Build;

        public DotNetTestCaseResult(TestRunInfo testRunInfo, HelixWorkItem helixWorkItem, TestCaseResult testCaseResult)
        {
            Debug.Assert(HelixUtil.TryGetHelixInfo(testCaseResult) == helixWorkItem.HelixInfo);
            TestRunInfo = testRunInfo;
            HelixWorkItem = helixWorkItem;
            TestCaseResult = testCaseResult;
        }

        public DotNetTestCaseResult(TestRunInfo testRunInfo, TestCaseResult testCaseResult)
        {
            TestRunInfo = testRunInfo;
            TestCaseResult = testCaseResult;
        }
    }
}
