using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public sealed class GitHubIssueKeyTests
    {
        [Theory]
        [InlineData("https://github.com/dotnet/runtime/issues/42677", "dotnet", "runtime", 42677)]
        [InlineData("https://github.com/dotnet/runtime/issues/42677/", "dotnet", "runtime", 42677)]
        [InlineData("https://github.com/dotnet/blah/issues/1/", "dotnet", "blah", 1)]
        public void TryCreateTests(string uri, string organization, string repository, int number)
        {
            Assert.True(GitHubIssueKey.TryCreateFromUri(uri, out var key));
            Assert.Equal(organization, key.Organization);
            Assert.Equal(repository, key.Repository);
            Assert.Equal(number, key.Number);
        }
    }
}
