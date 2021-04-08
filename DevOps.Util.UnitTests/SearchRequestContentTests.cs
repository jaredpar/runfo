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
        [InlineData("kind:pullrequest", "started:~7 kind:pullrequest")]
        [InlineData("kind:pr", "started:~7 kind:pr")]
        [InlineData("kind:mpr", "started:~7 kind:mpr")]
        [InlineData("repository:roslyn kind:mpr", "started:~7 kind:mpr repository:roslyn")]
        [InlineData("issues:false kind:rolling result:failed", "started:~7 result:failed kind:rolling issues:false")]
        public void BuildsRoundTrip(string toParse, string userQuery)
        {
            var options = new SearchBuildsRequest();
            options.ParseQueryString(toParse);
            Assert.Equal(userQuery, options.GetQueryString());
        }

        [Theory]
        [InlineData("message:error", "started:~7 message:\"error\"")]
        [InlineData("message:\"error again\"", "started:~7 message:\"error again\"")]
        [InlineData("name:test", "started:~7 name:\"test\"")]
        [InlineData("name:test message:error", "started:~7 name:\"test\" message:\"error\"")]
        public void TestsRoundTrip(string toParse, string userQuery)
        {
            var options = new SearchTestsRequest();
            options.ParseQueryString(toParse);
            Assert.Equal(userQuery, options.GetQueryString());
        }

        [Theory]
        [InlineData("message:error")]
        [InlineData("message:\"error again\"")]
        [InlineData("name:test message:\"error again\"")]
        [InlineData("started:~7 message:\"error again\"")]
        public void ParseDoesNotReset(string toParse)
        {
            var request = new SearchTestsRequest();
            request.ParseQueryString(toParse);
            var oldMessage = request.Message;
            var oldStarted = request.Started;
            var oldName = request.Name;
            request.ParseQueryString("targetBranch:main");
            Assert.Equal(oldMessage, request.Message);
            Assert.Equal(oldStarted, request.Started);
            Assert.Equal(oldName, request.Name);
            Assert.Equal("main", request.TargetBranch!.Value.Text);
        }

        [Theory]
        [InlineData("text:\"error\"", "started:~7 text:\"error\"")]
        [InlineData("text:\"error and space\"", "started:~7 text:\"error and space\"")]
        [InlineData("text:\"error and space\"   ", "started:~7 text:\"error and space\"")]
        [InlineData("displayName:Installer   ", "started:~7 displayName:\"Installer\"")]
        [InlineData("taskName:Installer   ", "started:~7 taskName:\"Installer\"")]
        [InlineData("taskName:Task displayName:Display", "started:~7 displayName:\"Display\" taskName:\"Task\"")]
        [InlineData("error", "started:~7 text:\"error\"")]
        [InlineData("text:error", "started:~7 text:\"error\"")]
        [InlineData("started:~3 kind:mpr text:error", "started:~3 kind:mpr text:\"error\"")]
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
