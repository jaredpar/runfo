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
        [InlineData("rolling", ModelBuildKind.Rolling, BuildTypeRequestKind.Equals, "rolling")]
        [InlineData("!rolling", ModelBuildKind.Rolling, BuildTypeRequestKind.NotEquals, "rolling")]
        [InlineData("pr", ModelBuildKind.PullRequest, BuildTypeRequestKind.Equals, "pr")]
        [InlineData("!pr", ModelBuildKind.PullRequest, BuildTypeRequestKind.NotEquals, "pr")]
        [InlineData("pullRequest", ModelBuildKind.PullRequest, BuildTypeRequestKind.Equals, "pullRequest")]
        [InlineData("mpr", ModelBuildKind.MergedPullRequest, BuildTypeRequestKind.Equals, "mpr")]
        [InlineData("all", ModelBuildKind.All, BuildTypeRequestKind.Equals, "all")]
        public void BuildTypeRequests(string value, ModelBuildKind buildType, BuildTypeRequestKind kind, string name)
        {
            var defaultKind = BuildTypeRequestKind.Equals;
            var request = BuildTypeRequest.Parse(value, defaultKind: defaultKind);
            Assert.Equal(buildType, request.BuildType);
            Assert.Equal(kind, request.Kind);
            Assert.Equal(name, request.BuildTypeName);
            Assert.Equal(value, request.GetQueryValue(defaultKind));
        }
    }
}
