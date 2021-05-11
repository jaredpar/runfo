using DevOps.Util;
using System.Diagnostics;

namespace DevOps.Util.DotNet
{
    public sealed class DotNetTestCaseResult
    {
        /// <summary>
        /// The TestCaseResult representing the actual test failure
        /// </summary>
        public TestCaseResult TestCaseResult { get; }

        /// <summary>
        /// Helix information for the test case if this was executed in Helix
        /// </summary>
        public HelixInfo? HelixInfo { get; }

        /// <summary>
        /// Is this the test case result that represents the helix work item. There is one of these 
        /// per WorkItem that helix runs
        /// </summary>
        public bool IsHelixWorkItem { get; }

        public bool IsHelixTestResult => HelixInfo.HasValue;

        public string TestCaseTitle => TestCaseResult.TestCaseTitle;

        public DotNetTestCaseResult(TestCaseResult testCaseResult, HelixInfo? helixInfo = null, bool isHelixWorkItem = false)
        {
            Debug.Assert(!isHelixWorkItem || (helixInfo is { } info && info == HelixUtil.TryGetHelixInfo(testCaseResult)));
            TestCaseResult = testCaseResult;
            HelixInfo = helixInfo;
            IsHelixWorkItem = IsHelixWorkItem;
        }
    }
}
