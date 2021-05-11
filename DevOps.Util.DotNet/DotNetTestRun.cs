using DevOps.Util;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Linq;

namespace DevOps.Util.DotNet
{
    public sealed class DotNetTestRun
    {
        public string ProjectName { get; }

        /// <summary>
        /// The id in Azure that represents this <see cref="TestRun"/>
        /// </summary>
        public int TestRunId { get; }

        public string TestRunName { get; }

        public ReadOnlyCollection<DotNetTestCaseResult> TestCaseResults { get; }

        public bool HasHelixWorkItem => TestCaseResults.Any(static x => x.IsHelixWorkItem);

        public DotNetTestRun(string projectName, int testRunId, string testRunName, ReadOnlyCollection<DotNetTestCaseResult> testCaseResults)
        {
            ProjectName = projectName;
            TestRunId = testRunId;
            TestRunName = testRunName;
            TestCaseResults = testCaseResults;
        }
    }
}