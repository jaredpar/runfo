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
        public HelixInfoWorkItem? HelixWorkItem { get; }

        /// <summary>
        /// Is this the test case result that represents the helix work item. There is one of these 
        /// per WorkItem that helix runs
        /// </summary>
        public bool IsHelixWorkItem { get; }

        public bool IsHelixTestResult => HelixWorkItem.HasValue;

        public string TestCaseTitle => TestCaseResult.TestCaseTitle;

        public DotNetTestCaseResult(TestCaseResult testCaseResult, HelixInfoWorkItem? helixWorkItem = null, bool isHelixWorkItem = false)
        {
            Debug.Assert(!isHelixWorkItem || (helixWorkItem is { } info && info == HelixUtil.TryGetHelixWorkItem(testCaseResult)));
            TestCaseResult = testCaseResult;
            HelixWorkItem = helixWorkItem;
            IsHelixWorkItem = IsHelixWorkItem;
        }
    }
}
