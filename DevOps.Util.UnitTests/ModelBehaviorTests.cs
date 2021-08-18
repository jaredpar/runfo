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
    public class ModelBehaviorTests : StandardTestBase
    {
        public ModelBehaviorTests(DatabaseFixture databaseFixture, ITestOutputHelper testOutputHelper)
            : base(databaseFixture, testOutputHelper)
        {

        }

        /// <summary>
        /// Ensure that deleting a <see cref="ModelBuild"/> will correctly delete all of the dependent 
        /// items as well.
        /// </summary>
        [Fact]
        public async Task DeleteModelBuild1()
        {
            var def = AddBuildDefinition("roslyn");
            var build = await AddBuildAsync(def);
            await AddAttemptAsync(1, build);
            ResetContext();

            Context.ModelBuilds.Remove(build);
            await Context.SaveChangesAsync();
            Assert.Equal(0, await Context.ModelBuilds.CountAsync());
            Assert.Equal(0, await Context.ModelBuildAttempts.CountAsync());
        }

        [Fact]
        public async Task DeleteModelBuild2()
        {
            var def = AddBuildDefinition("roslyn");
            var build = await AddBuildAsync(def);
            var attempt = await AddAttemptAsync(1, build);
            await AddTestRunAsync(
                attempt,
                "Windows Debug");

            ResetContext();
            build = await Context.ModelBuilds.Where(x => x.Id == build.Id).SingleAsync();
            Context.ModelBuilds.Remove(build);
            await Context.SaveChangesAsync();
            Assert.Equal(0, await Context.ModelBuilds.CountAsync());
            Assert.Equal(0, await Context.ModelBuildAttempts.CountAsync());
            Assert.Equal(0, await Context.ModelTestRuns.CountAsync());
        }

        [Fact]
        public async Task DeleteModelBuild3()
        {
            var def = AddBuildDefinition("roslyn");
            var build = await AddBuildAsync(def);
            var attempt = await AddAttemptAsync(1, build);
            await AddTestRunAsync(
                attempt,
                "Windows Debug",
                ("testCase1", "error"),
                ("testCase2", "other error"));

            ResetContext();
            build = await Context.ModelBuilds.Where(x => x.Id == build.Id).SingleAsync();
            Context.ModelBuilds.Remove(build);
            await Context.SaveChangesAsync();
            Assert.Equal(0, await Context.ModelBuilds.CountAsync());
            Assert.Equal(0, await Context.ModelBuildAttempts.CountAsync());
            Assert.Equal(0, await Context.ModelTestRuns.CountAsync());
            Assert.Equal(0, await Context.ModelTestResults.CountAsync());
        }
    }
}
