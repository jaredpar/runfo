using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using DevOps.Util;
using DevOps.Util.DotNet;

namespace Runfo
{

    // TODO: make this type use actual dictionaries and hashes instead of crappy lists.
    // just wrote this for functionality at the moment. Perf fix ups later.
    internal sealed class BuildTestInfo
    {
        public List<DotNetTestRun> DataList { get; }

        public Build Build { get; }

        internal BuildTestInfo(Build build, List<DotNetTestRun> dataList)
        {
            Build = build;
            DataList = dataList;
        }

        internal IEnumerable<string> GetTestCaseTitles() => DataList
            .SelectMany(x => x.TestCaseResults)
            .Select(x => x.TestCaseTitle)
            .Distinct()
            .OrderBy(x => x);

        internal IEnumerable<string> GetTestRunNames() => DataList
            .Select(x => x.TestRunName)
            .Distinct()
            .OrderBy(x => x);

        internal IEnumerable<string> GetTestRunNamesForTestCaseTitle(string testCaseTitle) => DataList
            .Where(x => x.TestCaseResults.Any(x => x.TestCaseTitle == testCaseTitle))
            .Select(x => x.TestRunName)
            .Distinct()
            .OrderBy(x => x);

        internal IEnumerable<DotNetTestCaseResult> GetDotNetTestCaseResultForTestCaseTitle(string testCaseTitle) => DataList
            .SelectMany(x => x.TestCaseResults)
            .Where(x => x.TestCaseTitle == testCaseTitle);

        internal IEnumerable<DotNetTestCaseResult> GetDotNetTestCaseResultForTestRunName(string testRunName) => DataList
            .Where(x => x.TestRunName == testRunName)
            .SelectMany(x => x.TestCaseResults);

        internal IEnumerable<HelixInfo> GetHelixWorkItems() => DataList
            .SelectMany(x => x.TestCaseResults)
            .SelectNullableValue(x => x.HelixInfo)
            .GroupBy(x => x.WorkItemName)
            .Select(x => x.First())
            .OrderBy(x => x.JobId);

        internal bool ContainsTestCaseTitle(string testCaseTitle) => GetTestCaseTitles().Contains(testCaseTitle);

        internal bool ContainsTestRunName(string testRunName) => DataList.Exists(x => x.TestRunName == testRunName);

        internal BuildTestInfo FilterToTestCaseTitle(Regex testCaseTitleRegex)
        {
            var list = new List<DotNetTestRun>();
            foreach (var testRun in DataList)
            {
                list.Add(new DotNetTestRun(
                    testRun.ProjectName,
                    testRun.TestRunId,
                    testRun.TestRunName,
                    testRun.TestCaseResults.Where(x => testCaseTitleRegex.IsMatch(x.TestCaseTitle)).ToList().ToReadOnlyCollection()));
            }
            return new BuildTestInfo(Build, list);
        }

        internal BuildTestInfo FilterToTestRunName(Regex testRunNameRegex)
        {
            var dataList = DataList
                .Where(x => testRunNameRegex.IsMatch(x.TestRunName))
                .ToList();
            return new BuildTestInfo(Build, dataList);
        }

        public override string ToString() => Build.Id.ToString();
    }

    internal sealed class BuildTestInfoCollection : IEnumerable<BuildTestInfo>
    {
        public ReadOnlyCollection<BuildTestInfo> BuildTestInfos { get; }

        public BuildTestInfoCollection(ReadOnlyCollection<BuildTestInfo> buildTestInfos)
        {
            BuildTestInfos = buildTestInfos;
        }

        public BuildTestInfoCollection(IEnumerable<BuildTestInfo> buildTestInfos)
            : this(new ReadOnlyCollection<BuildTestInfo>(buildTestInfos.ToList()))
        {

        }

        public List<string> GetTestCaseTitles() => BuildTestInfos
            .SelectMany(x => x.GetTestCaseTitles())
            .Distinct()
            .ToList();

        internal List<DotNetTestCaseResult> GetDotNetTestCaseResultForTestCaseTitle(string testCaseTitle) =>
            BuildTestInfos
                .SelectMany(x => x.GetDotNetTestCaseResultForTestCaseTitle(testCaseTitle))
                .ToList();

        internal List<DotNetTestCaseResult> GetDotNetTestCaseResultForTestRunName(string testRunName) =>
            BuildTestInfos
                .SelectMany(x => x.GetDotNetTestCaseResultForTestRunName(testRunName))
                .ToList();

        internal List<Build> GetBuildsForTestCaseTitle(string testCaseTitle) => this
            .GetBuildTestInfosForTestCaseTitle(testCaseTitle)
            .Select(x => x.Build)
            .ToList();

        internal List<BuildTestInfo> GetBuildTestInfosForTestCaseTitle(string testCaseTitle) => BuildTestInfos
            .Where(x => x.ContainsTestCaseTitle(testCaseTitle))
            .OrderBy(x => x.Build.Id)
            .ToList();

        internal List<string> GetTestRunNamesForTestCaseTitle(string testCaseTitle) => BuildTestInfos
            .SelectMany(x => x.GetTestRunNamesForTestCaseTitle(testCaseTitle))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        internal List<string> GetTestRunNames() => BuildTestInfos
            .SelectMany(x => x.DataList.Select(x => x.TestRunName))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        internal BuildTestInfoCollection FilterToTestCaseTitle(Regex testCaseTitleRegex)
        {
            var buildTestInfos = BuildTestInfos
                .Select(x => x.FilterToTestCaseTitle(testCaseTitleRegex))
                .Where(x => x.DataList.Count > 0);
            return new BuildTestInfoCollection(buildTestInfos);
        }

        internal BuildTestInfoCollection FilterToTestRunName(Regex testRunName)
        {
            var buildTestInfos = BuildTestInfos
                .Select(x => x.FilterToTestRunName(testRunName))
                .Where(x => x.DataList.Count > 0);
            return new BuildTestInfoCollection(buildTestInfos);
        }

        internal BuildTestInfoCollection Filter(Func<BuildTestInfo, bool> predicate)
        {
            var buildTestInfos = BuildTestInfos.Where(predicate);
            return new BuildTestInfoCollection(buildTestInfos);
        }

        public IEnumerator<BuildTestInfo> GetEnumerator() => BuildTestInfos.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
