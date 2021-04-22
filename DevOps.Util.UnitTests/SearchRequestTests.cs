using DevOps.Util.DotNet.Triage;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DevOps.Util.UnitTests
{
    public class SearchRequestTests : StandardTestBase
    {
        public SearchRequestTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {

        }

        [Fact]
        public async Task BuildResultSearches()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            AddBuild("1|dotnet|roslyn||Failed", def);
            AddBuild("2|dotnet|roslyn||Failed", def);
            AddBuild("3|dotnet|roslyn||Succeeded", def);
            AddBuild("4|dotnet|roslyn||Canceled", def);
            await Context.SaveChangesAsync();

            await Test(2, "result:failed");
            await Test(1, "result:succeeded");
            await Test(1, "result:canceled");

            async Task Test(int count, string value)
            {
                var request = new SearchBuildsRequest();
                request.ParseQueryString(value);
                var query = request.Filter(Context.ModelBuilds);
                var queryCount = await query.CountAsync();
                Assert.Equal(count, queryCount);
            }
        }

        [Fact]
        public async Task BuildIssuesSearches()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var build1 = AddBuild("1", def);
            var build2 = AddBuild("2", def);
            await Test(2, "issues:false");
            await Test(0, "issues:true");

            AddGitHubIssue("", build1);
            await Test(1, "issues:false");
            await Test(1, "issues:true");

            AddGitHubIssue("", build2);
            await Test(0, "issues:false");
            await Test(2, "issues:true");

            async Task Test(int count, string value)
            {
                await Context.SaveChangesAsync();
                var request = new SearchBuildsRequest();
                request.ParseQueryString(value);
                var query = request.Filter(Context.ModelBuilds);
                var queryCount = await query.CountAsync();
                Assert.Equal(count, queryCount);
            }
        }

        [Fact]
        public async Task TestResultSearchMessage()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var build1 = AddBuild("1|dotnet|roslyn||Failed", def);
            var testRun1 = AddTestRun("windows", AddAttempt(1, build1));
            AddTestResult("Test1||||cat", testRun1);
            AddTestResult("Test2||||cat", testRun1);
            AddTestResult("Test3||||dog", testRun1);
            var build2 = AddBuild("2|dotnet|roslyn||Failed", def);
            var testRun2 = AddTestRun("windows", AddAttempt(1, build2));
            AddTestResult("Test1||||fish", testRun1);
            AddTestResult("Test2||||fish", testRun1);
            AddTestResult("Test3||||cat", testRun1);
            await Context.SaveChangesAsync();

            await Test(3, "message:#cat");
            await Test(1, "message:#dog");
            await Test(2, "message:#fish");
            await Test(0, "message:#tree");

            async Task Test(int count, string value)
            {
                var request = new SearchTestsRequest();
                request.ParseQueryString(value);
                var query = request.Filter(Context.ModelTestResults);
                var queryCount = await query.CountAsync();
                Assert.Equal(count, queryCount);
            }
        }

        [Fact]
        public async Task TimelineRespectsStartedOption()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var build = AddBuild("1|dotnet|roslyn|2020-01-01|Failed", def);
            AddTimelineIssue("build|dog", AddAttempt(1, build));
            await Context.SaveChangesAsync();

            var request = new SearchTimelinesRequest("text:#dog")
            {
                Started = null,
            };

            Assert.Equal(1, await request.Filter(Context.ModelTimelineIssues).CountAsync());

            // By default the Started value will be seven days ago which won't match
            // the date of the build here
            request = new SearchTimelinesRequest("text:#dog");
            Assert.Equal(0, await request.Filter(Context.ModelTimelineIssues).CountAsync());
        }
    }
}
