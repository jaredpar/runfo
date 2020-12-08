﻿using DevOps.Util;
using DevOps.Util.DotNet.Triage;
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

        /// <summary>
        /// This is only used when creating links for the test name
        /// </summary>
        public SearchBuildsRequest? BuildsRequest { get; set; }

        public bool IncludeTestFullNameColumn { get; set; }

        public bool IncludeErrorMessageColumn { get; set; }

        public bool IncludeBuildColumn { get; set; }

        public bool IncludeBuildKindColumn { get; set; }

        public bool IncludeHelixColumns { get; set; }

        public bool IncludeTestFullNameLinks { get; set; }

        public string? GitHubRepository { get; set; }

        public TestResultsDisplay()
        {
        }

        public TestResultsDisplay(IEnumerable<ModelTestResult> modelTestResults)
        {
            var anyHelix = false;
            string? gitHubRepository = null;
            var emptyDictionary = new Dictionary<string, string>();
            foreach (var modelTestResult in modelTestResults)
            {
                anyHelix = anyHelix || modelTestResult.IsHelixTestResult;
                gitHubRepository ??= modelTestResult.ModelBuild.GitHubRepository;

                var routeData = new Dictionary<string, string>();
                if (BuildsRequest is object)
                {
                    routeData["bq"] = BuildsRequest.GetQueryString();
                }

                var request = new SearchTestsRequest()
                {
                    Name = modelTestResult.TestFullName
                };

                routeData["tq"] = request.GetQueryString();

                var testResultInfo = new TestResultInfo()
                {
                    BuildNumber = modelTestResult.ModelBuild.BuildNumber,
                    BuildUri = DevOpsUtil.GetBuildUri(modelTestResult.ModelBuild.AzureOrganization, modelTestResult.ModelBuild.AzureProject, modelTestResult.ModelBuild.BuildNumber),
                    Kind = modelTestResult.ModelBuild.GetModelBuildKind().GetDisplayString(),
                    TestRun = modelTestResult.TestRunName,
                    TestFullName = modelTestResult.TestFullName,
                    TestFullNameRouteData = routeData,
                    ErrorMessage = modelTestResult.ErrorMessage,
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
            public string? TestFullName { get; set; }
            public string? ErrorMessage { get; set; }
            public Dictionary<string, string>? TestFullNameRouteData { get; set; }
            public string? Kind { get; set; }
            public string? BuildUri { get; set; }
            public string? HelixConsoleUri { get; set; }
            public string? HelixRunClientUri { get; set; }
            public string? HelixCoreDumpUri { get; set; }
            public string? HelixTestResultsUri { get; set; }
        }
    }
}
