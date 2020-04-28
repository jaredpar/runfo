#nullable enable

using DevOps.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet
{
    public static class DotNetUtil
    {
        public static readonly string Organization = "dnceng";

        public static readonly (string BuildName, string Project, int DefinitionId)[] BuildDefinitions = new[]
            {
                ("runtime", "public", 686),
                ("runtime-official", "internal", 679),
                ("coreclr", "public", 655),
                ("libraries", "public", 675),
                ("libraries windows", "public", 676),
                ("libraries linux", "public", 677),
                ("libraries osx", "public", 678),
                ("crossgen2", "public", 701),
                ("roslyn", "public", 15),
                ("roslyn-integration", "public", 245),
                ("aspnet", "public", 278),
                ("aspnet-official", "internal", 21),
                ("sdk", "public", 136),
                ("winforms", "public", 267),
            };

        public static BuildDefinitionKey? GetBuildDefinitionKeyFromFriendlyName(string name)
        {
            var item = BuildDefinitions.FirstOrDefault(x => x.BuildName == name);
            if (item.Project is object)
            {
                return new BuildDefinitionKey(Organization, item.Project, item.DefinitionId);
            }

            return null;
        }

        public static string GetDefinitionName(Build build) => 
            TryGetDefinitionName(build, out var name) 
                ? name
                : build.Definition.Name.ToString();

        public static bool TryGetDefinitionName(Build build, [NotNullWhen(true)] out string? name)
        {
            var project = build.Project.Name;
            var id = build.Definition.Id;
            foreach (var tuple in BuildDefinitions)
            {
                if (tuple.Project == project && tuple.DefinitionId == id)
                {
                    name = tuple.BuildName;
                    return true;
                }
            }

            name = null;
            return false;
        }

        public static bool TryGetDefinitionId(string definition, string defaultProject, [NotNullWhen(true)] out string? project, out int definitionId)
        {
            definitionId = 0;
            project = null;

            if (definition is null)
            {
                return false;
            }

            var index = definition.IndexOf(':');
            if (index >= 0)
            {
                var both = definition.Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
                definition = both[0];
                project = both[1]!;
            }
            else
            {
                project = defaultProject;
            }

            if (int.TryParse(definition, out definitionId))
            {
                return true;
            }

            foreach (var (name, p, id) in DotNetUtil.BuildDefinitions)
            {
                if (name == definition)
                {
                    definitionId = id;
                    project = p;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Normalize the branch name so that has the short human readable form of the branch
        /// name
        /// </summary>
        public static string NormalizeBranchName(string fullName) => BranchName.Parse(fullName).ShortName;

        public static async Task<List<DotNetTestRun>> ListDotNetTestRunsAsync(DevOpsServer server, Build build, params TestOutcome[]? outcomes)
        {
            var testRuns = await server.ListTestRunsAsync(build.Project.Name, build.Id).ConfigureAwait(false);
            var taskList = new List<Task<DotNetTestRun>>();
            foreach (var testRun in testRuns)
            {
                taskList.Add(GetDotNetTestRunAsync(server, build, testRun, outcomes));
            }

            await Task.WhenAll(taskList);
            var list = new List<DotNetTestRun>();
            foreach (var task in taskList)
            {
                list.Add(task.Result);
            }

            return list;

            static async Task<DotNetTestRun> GetDotNetTestRunAsync(DevOpsServer server, Build build, TestRun testRun, TestOutcome[]? outcomes)
            {
                var all = await server.ListTestResultsAsync(build.Project.Name, testRun.Id, outcomes: outcomes).ConfigureAwait(false);
                var info = new TestRunInfo(build, testRun);
                var list = ToDotNetTestCaseResult(info, all.ToList());
                return new DotNetTestRun(info, new ReadOnlyCollection<DotNetTestCaseResult>(list));
            }

            static List<DotNetTestCaseResult> ToDotNetTestCaseResult(TestRunInfo testRunInfo, List<TestCaseResult> testCaseResults)
            {
                var list = new List<DotNetTestCaseResult>();
                foreach (var testCaseResult in testCaseResults)
                {
                    var helixInfo = HelixUtil.TryGetHelixInfo(testCaseResult);
                    if (helixInfo is null)
                    {
                        list.Add(new DotNetTestCaseResult(testRunInfo, testCaseResult));
                        continue;
                    }

                    if (HelixUtil.IsHelixWorkItem(testCaseResult))
                    {
                        var helixWorkItem = new HelixWorkItem(testRunInfo, helixInfo.Value, testCaseResult);
                        list.Add(new DotNetTestCaseResult(testRunInfo, helixWorkItem, testCaseResult));
                    }
                    else
                    {
                        var workItemTestCaseResult = testCaseResults.FirstOrDefault(x => HelixUtil.IsHelixWorkItemAndTestCaseResult(workItem: x, test: testCaseResult));
                        if (workItemTestCaseResult is null)
                        {
                            // This can happen when helix errors and doesn't fully upload a result. Treat it like
                            // a normal test case
                            list.Add(new DotNetTestCaseResult(testRunInfo, testCaseResult));
                        }
                        else
                        {
                            var helixWorkItem = new HelixWorkItem(testRunInfo, helixInfo.Value, workItemTestCaseResult);
                            list.Add(new DotNetTestCaseResult(testRunInfo, helixWorkItem, testCaseResult));
                        }
                    }
                }

                return list;
            }
        }
    }
}
