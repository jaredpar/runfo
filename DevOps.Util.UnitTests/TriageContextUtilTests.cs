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
            var build1 = await CreateBuildAsync("1");
            var build2 = await CreateBuildAsync("2");
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

            async Task<ModelBuild> CreateBuildAsync(string buildId)
            {
                var build = await AddBuildAsync(buildId, def);
                var attempt1 = await AddAttemptAsync(
                    build,
                    1,
                    ("windows", "failed", null));
                await AddAttemptAsync(
                    build,
                    2,
                    ("windows", "blah", null));
                await AddTestRunAsync(
                    attempt1,
                    "windows",
                    ("xml", null),
                    ("json", null),
                    ("yaml", null));
                return build;
            }
        }
    }
}
