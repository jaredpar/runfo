using DevOps.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet
{
    public static class DotNetUtil
    {
        /// <summary>
        /// Normalize the branch name so that has the short human readable form of the branch
        /// name
        /// </summary>
        public static string NormalizeBranchName(string fullName) => BranchName.Parse(fullName).ShortName;

        public static async Task DoWithTransactionAsync(SqlConnection connection, string transactionName, Func<SqlTransaction, Task> process)
        {
            await connection.EnsureOpenAsync();
            var transaction = connection.BeginTransaction(transactionName);

            try
            {
                await process(transaction);
                transaction.Commit();
            }
            catch (Exception)
            {
                // Attempt to roll back the transaction. 
                try
                {
                    transaction.Rollback();
                }
                catch (Exception)
                {
                    // Expected that this will fail if the transaction fails on the server
                }

                throw;
            }
        }

        public static async Task DoWithTransactionAsync(SqlConnection connection, string transactionName, Func<SqlTransaction, SqlCommand, Task> process)
        {
            await DoWithTransactionAsync(connection, transactionName, async transaction =>
            {
                using var command = connection.CreateCommand();
                command.Connection = connection;
                command.Transaction = transaction;
                await process(transaction, command);
            });
        }

        public static ILogger CreateConsoleLogger()
        {
            var provider = new ConsoleLoggerProvider((message, level) => true, includeScopes: true);
            return provider.CreateLogger("Console logger");
        }

        public static async Task<List<DotNetTestRun>> ListDotNetTestRunsAsync(DevOpsServer server, Build build, params TestOutcome[] outcomes)
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

            static async Task<DotNetTestRun> GetDotNetTestRunAsync(DevOpsServer server, Build build, TestRun testRun, TestOutcome[] outcomes)
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
