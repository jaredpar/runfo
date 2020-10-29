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
        [InlineData("issues:false kind:rolling result:failed", "kind:rolling result:failed issues:false")]
        public void BuildsRoundTrip(string toParse, string userQuery)
        {
            var options = new SearchBuildsRequest();
            options.ParseQueryString(toParse);
            Assert.Equal(userQuery, options.GetQueryString());
        }

        [Theory]
        [InlineData("message:error", "message:\"error\"")]
        [InlineData("message:\"error again\"", "message:\"error again\"")]
        [InlineData("name:test", "name:\"test\"")]
        [InlineData("name:test message:error", "name:\"test\" message:\"error\"")]
        public void TestsRoundTrip(string toParse, string userQuery)
        {
            var options = new SearchTestsRequest();
            options.ParseQueryString(toParse);
            Assert.Equal(userQuery, options.GetQueryString());
        }

        [Theory]
        [InlineData("text:\"error\"", "text:\"error\"")]
        [InlineData("text:\"error and space\"", "text:\"error and space\"")]
        [InlineData("text:\"error and space\"   ", "text:\"error and space\"")]
        [InlineData("error", "text:\"error\"")]
        [InlineData("text:error", "text:\"error\"")]
        public void TimelineRoundTrip(string toParse, string userQuery)
        {
            var options = new SearchTimelinesRequest();
            options.ParseQueryString(toParse);
            Assert.Equal(userQuery, options.GetQueryString());
        }


        [Theory]
        [InlineData("text:\"error\"", "text:\"error\"")]
        [InlineData("text:error", "text:\"error\"")]
        [InlineData("text:\"error\" logKind:console", "logKind:console text:\"error\"")]
        public void HelixLogsRoundTrip(string toParse, string userQuery)
        {
            var options = new SearchHelixLogsRequest();
            options.ParseQueryString(toParse);
            Assert.Equal(userQuery, options.GetQueryString());
        }
    }
}
