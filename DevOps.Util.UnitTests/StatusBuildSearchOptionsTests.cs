using DevOps.Status.Util;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public class StatusBuildSearchOptionsTests
    {
        [Theory]
        [InlineData("count:10", "")]
        [InlineData("count:11", "count:11")]
        [InlineData("kind:pullrequest", "kind:pr")]
        [InlineData("kind:pr", "kind:pr")]
        [InlineData("kind:mpr", "kind:mpr")]
        [InlineData("repository:roslyn kind:mpr", "repository:roslyn kind:mpr")]
        [InlineData("count:1 repository:roslyn kind:mpr", "repository:roslyn kind:mpr count:1")]
        public void RoundTripQueryString(string toParse, string userQuery)
        {
            var options = new SearchBuildsRequest();
            options.ParseQueryString(toParse);
            Assert.Equal(userQuery, options.GetQueryString());
        }
    }
}
