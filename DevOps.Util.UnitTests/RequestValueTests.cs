using DevOps.Util.Triage;
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
        [InlineData("rolling", ModelBuildKind.Rolling, EqualsKind.Equals, "rolling")]
        [InlineData("!rolling", ModelBuildKind.Rolling, EqualsKind.NotEquals, "rolling")]
        [InlineData("pr", ModelBuildKind.PullRequest, EqualsKind.Equals, "pr")]
        [InlineData("!pr", ModelBuildKind.PullRequest, EqualsKind.NotEquals, "pr")]
        [InlineData("pullRequest", ModelBuildKind.PullRequest, EqualsKind.Equals, "pullRequest")]
        [InlineData("mpr", ModelBuildKind.MergedPullRequest, EqualsKind.Equals, "mpr")]
        [InlineData("all", ModelBuildKind.All, EqualsKind.Equals, "all")]
        public void BuildTypeRequests(string value, ModelBuildKind buildType, EqualsKind kind, string name)
        {
            var defaultKind = EqualsKind.Equals;
            var request = BuildTypeRequestValue.Parse(value, defaultKind: defaultKind);
            Assert.Equal(buildType, request.BuildType);
            Assert.Equal(kind, request.Kind);
            Assert.Equal(name, request.BuildTypeName);
            Assert.Equal(value, request.GetQueryValue(defaultKind));
        }
    }
}
