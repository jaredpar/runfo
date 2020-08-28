using DevOps.Util;
using DevOps.Util.Triage;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevOps.Status.Util
{
    public class TestResultsDisplay
    {
        public static TestResultsDisplay Empty => new TestResultsDisplay();

        public List<TestResultInfo> Results { get; } = new List<TestResultInfo>();

        public bool IncludeHelixColumns { get; set; }

        public bool IncludeKindColumn { get; set; }

        public string? GitHubRepository { get; set; }

        public TestResultsDisplay()
        {
        }

        public TestResultsDisplay(IEnumerable<ModelTestResult> modelTestResults)
        {
            var anyHelix = false;
            string? gitHubRepository = null;
            foreach (var modelTestResult in modelTestResults)
            {
                anyHelix = anyHelix || modelTestResult.IsHelixTestResult;
                gitHubRepository ??= modelTestResult.ModelBuild.GitHubRepository;

                var testResultInfo = new TestResultInfo()
                {
                    BuildNumber = modelTestResult.ModelBuild.BuildNumber,
                    BuildUri = DevOpsUtil.GetBuildUri(modelTestResult.ModelBuild.ModelBuildDefinition.AzureOrganization, modelTestResult.ModelBuild.ModelBuildDefinition.AzureProject, modelTestResult.ModelBuild.BuildNumber),
                    Kind = modelTestResult.ModelBuild.GetModelBuildKind().GetDisplayString(),
                    TestRun = modelTestResult.ModelTestRun.Name,
                    HelixConsoleUri = modelTestResult.HelixConsoleUri,
                    HelixRunClientUri = modelTestResult.HelixRunClientUri,
                    HelixCoreDumpUri = modelTestResult.HelixCoreDumpUri,
                    HelixTestResultsUri = modelTestResult.HelixTestResultsUri,
                };
                Results.Add(testResultInfo);
            }

            IncludeHelixColumns = anyHelix;
            GitHubRepository = gitHubRepository;
        }

        public class TestResultInfo
        {
            public int BuildNumber { get; set; }

            public string? TestRun { get; set; }

            public string? Kind { get; set; }

            public string? BuildUri { get; set; }

            public string? HelixConsoleUri { get; set; }

            public string? HelixRunClientUri { get; set; }

            public string? HelixCoreDumpUri { get; set; }

            public string? HelixTestResultsUri { get; set; }
        }
    }
}
