using DevOps.Util.DotNet.Triage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public class RequestValueTests
    {
        [Theory]
        [InlineData("rolling", ModelBuildKind.Rolling, EqualsKind.Equals, "rolling", null)]
        [InlineData("!rolling", ModelBuildKind.Rolling, EqualsKind.NotEquals, "rolling", null)]
        [InlineData("pr", ModelBuildKind.PullRequest, EqualsKind.Equals, "pr", null)]
        [InlineData("!pr", ModelBuildKind.PullRequest, EqualsKind.NotEquals, "pr", null)]
        [InlineData("pullRequest", ModelBuildKind.PullRequest, EqualsKind.Equals, "pullRequest", null)]
        [InlineData("mpr", ModelBuildKind.MergedPullRequest, EqualsKind.Equals, "mpr", null)]
        [InlineData("all", ModelBuildKind.All, EqualsKind.Equals, "all", null)]
        public void BuildTypeRequestValues(string value, ModelBuildKind buildType, EqualsKind kind, string name, EqualsKind? defaultKind)
        {
            defaultKind ??= EqualsKind.Equals;
            var request = BuildTypeRequestValue.Parse(value, defaultKind: defaultKind.Value);
            Assert.Equal(buildType, request.BuildType);
            Assert.Equal(kind, request.Kind);
            Assert.Equal(name, request.BuildTypeName);
            Assert.Equal(value, request.GetQueryValue(defaultKind));
        }

        [Theory]
        [InlineData("~1", RelationalKind.GreaterThan, 1, null, null)]
        [InlineData("<~1", RelationalKind.LessThan, 1, null, null)]
        [InlineData("2020-09-15", RelationalKind.GreaterThan, null, null, null)]
        [InlineData("2020-9-15", RelationalKind.GreaterThan, null, "2020-09-15", null)]
        [InlineData("2020-9-1", RelationalKind.GreaterThan, null, "2020-09-01", null)]
        [InlineData("2020-09-15", RelationalKind.LessThan, null, null, RelationalKind.LessThan)]
        [InlineData("<2020-09-15", RelationalKind.LessThan, null, null, null)]
        public void DateRequestValues(string value, RelationalKind kind, int? dayQuery, string? queryValue, RelationalKind? defaultKind)
        {
            defaultKind ??= RelationalKind.GreaterThan;
            var request = DateRequestValue.Parse(value, defaultKind: defaultKind.Value);
            Assert.Equal(dayQuery, request.DayQuery);
            Assert.Equal(kind, request.Kind);
            Assert.Equal(queryValue ?? value, request.GetQueryValue(defaultKind));
        }
    }
}
