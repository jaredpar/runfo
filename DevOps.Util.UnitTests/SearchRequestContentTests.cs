using DevOps.Status.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public class SearchRequestContentTests
    {
        [Theory]
        [InlineData("started:~10", "started:~10")]
        [InlineData("started:~11", "started:~11")]
        [InlineData("kind:pullrequest", "kind:pullrequest")]
        [InlineData("kind:pr", "kind:pr")]
        [InlineData("kind:mpr", "kind:mpr")]
        [InlineData("repository:roslyn kind:mpr", "repository:roslyn kind:mpr")]
        public void BuildsRoundTrip(string toParse, string userQuery)
        {
            var options = new SearchBuildsRequest();
            options.ParseQueryString(toParse);
            Assert.Equal(userQuery, options.GetQueryString());
        }
    }
}
