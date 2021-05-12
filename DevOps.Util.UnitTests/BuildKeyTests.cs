using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public sealed class BuildKeyTests
    {
        /// <summary>
        /// Important that we maintain this format as it's depended on by the database schema
        /// </summary>
        [Theory]
        [InlineData("dnceng", "public", 42, "dnceng-public-42")]
        [InlineData("dnceng", "internal", 42, "dnceng-internal-42")]
        [InlineData("test", "internal", 13, "test-internal-13")]
        public void NameKeyRoundTrip(string organization, string project, int buildNumber, string nameKey)
        {
            var buildKey = new BuildKey(organization, project, buildNumber);
            Assert.Equal(nameKey, buildKey.NameKey);
            buildKey = BuildKey.FromNameKey(nameKey);
            Assert.Equal(organization, buildKey.Organization);
            Assert.Equal(project, buildKey.Project);
            Assert.Equal(buildNumber, buildKey.Number);
        }
    }
}
