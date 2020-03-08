using DevOps.Util;
using System.Diagnostics;

namespace DevOps.Util.DotNet
{
    public readonly struct HelixWorkItem
    {
        public readonly HelixInfo HelixInfo;

        public readonly TestRunInfo TestRunInfo;

        // This is the TestCaseResult that represents the Helix Work item
        public readonly TestCaseResult TestCaseResult;

        public string JobId => HelixInfo.JobId;

        public string WorkItemName => HelixInfo.WorkItemName;

        public Build Build => TestRunInfo.Build;

        public TestRun TestRun => TestRunInfo.TestRun;

        public string ProjectName => Build.Project.Name;

        public HelixWorkItem(TestRunInfo testRunInfo, HelixInfo helixInfo, TestCaseResult testCaseResult)
        {
            Debug.Assert(HelixUtil.TryGetHelixInfo(testCaseResult) == helixInfo);
            TestRunInfo = testRunInfo;
            HelixInfo = helixInfo;
            TestCaseResult = testCaseResult;
        }
    }
}
