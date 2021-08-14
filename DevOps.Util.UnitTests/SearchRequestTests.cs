using DevOps.Util.DotNet.Triage;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DevOps.Util.UnitTests
{
    [Collection(DatabaseCollection.Name)]
    public class SearchRequestTests : StandardTestBase
    {
        public SearchRequestTests(DatabaseFixture databaseFixture, ITestOutputHelper testOutputHelper)
            : base(databaseFixture, testOutputHelper)
        {

        }

        [Fact]
        public async Task BuildResultSearches()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            await AddBuildAsync("1|Failed|", def);
            await AddBuildAsync("2|Failed", def);
            await AddBuildAsync("3|Succeeded", def);
            await AddBuildAsync("4|Canceled", def);
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
            var build1 = await AddBuildAsync("1", def);
            var build2 = await AddBuildAsync("2", def);
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
            var build1 = await AddBuildAsync("1|Failed", def);
            await AddTestRunAsync(
                await AddAttemptAsync(1, build1),
                "windows",
                ("Test1", "cat"),
                ("Test2", "cat"),
                ("Test3", "dog"));
            var build2 = await AddBuildAsync("2|Failed", def);
            await AddTestRunAsync(
                await AddAttemptAsync(1, build2),
                "windows",
                ("Test1", "fish"),
                ("Test2", "fish"),
                ("Test3", "cat"));
            await Context.SaveChangesAsync();

            var testResults = await Context.ModelTestResults.ToListAsync();
            var errorMessages = await Context.ModelTestResults.Select(x => x.ErrorMessage).ToListAsync();
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
            var build = await AddBuildAsync("1|Failed|2020-01-01", def);
            await AddAttemptAsync(
                build,
                attempt: 1,
                ("build", "dog", null));
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
