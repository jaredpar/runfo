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
        public async Task DeleteModelBuild()
        {
            var def = AddBuildDefinition("roslyn");
            var build = AddBuild(def);
            var attempt = AddAttempt(1, build);
            var testRun = AddTestRun(attempt, "Windows Debug");
            AddTestResult("", testRun);

            Context.ModelBuilds.Remove(build);
            await Context.SaveChangesAsync();
            DatabaseFixture.AssertEmpty();
        }
    }
}
