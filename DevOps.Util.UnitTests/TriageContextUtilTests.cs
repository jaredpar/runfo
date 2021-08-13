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
    public class TriageContextUtilTests : StandardTestBase
    {
        public TriageContextUtilTests(DatabaseFixture databaseFixture, ITestOutputHelper testOutputHelper)
            : base(databaseFixture, testOutputHelper)
        {
        }

        [Fact]
        public async Task MarkMergedPullRequestTest()
        {
            var def = AddBuildDefinition("||roslyn|");
            var build1 = CreateBuild("1");
            var build2 = CreateBuild("2");
            await TriageContextUtil.MarkAsMergedPullRequestAsync(build1);
            await Verify(build1.Id, ModelBuildKind.MergedPullRequest);
            await Verify(build2.Id, ModelBuildKind.Rolling);

            async Task Verify(int modelBuildId, ModelBuildKind kind)
            {
                var attempts = await Context.ModelBuildAttempts.Where(x => x.ModelBuildId == modelBuildId).ToListAsync();
                Assert.Equal(2, attempts.Count);
                Assert.True(attempts.All(x => x.BuildKind == kind));

                var issues = await Context.ModelTimelineIssues.Where(x => x.ModelBuildId == modelBuildId).ToListAsync();
                Assert.Equal(2, issues.Count);
                Assert.True(issues.All(x => x.BuildKind == kind));

                var tests = await Context.ModelTestResults.Where(x => x.ModelBuildId == modelBuildId).ToListAsync();
                Assert.Equal(3, tests.Count);
                Assert.True(tests.All(x => x.BuildKind == kind));
            }

            ModelBuild CreateBuild(string buildId)
            {
                var build = AddBuild(buildId, def);
                var attempt1 = AddAttempt(1, build);
                var attempt2 = AddAttempt(2, build);
                AddTimelineIssue("windows|failed", attempt1);
                AddTimelineIssue("windows|blah", attempt2);
                var testRun = AddTestRun("windows", attempt1);
                AddTestResult("xml", testRun);
                AddTestResult("json", testRun);
                AddTestResult("yaml", testRun);
                return build;
            }
        }
    }
}
