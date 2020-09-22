
using Newtonsoft.Json;
using Org.BouncyCastle.Bcpg.OpenPgp;
using System.Linq;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public class DevOpsUtilTests
    {
        [Theory]
        [InlineData("https://dev.azure.com/dnceng/public/_build/results?buildId=626777", "dnceng")]
        [InlineData("https://dev.azure.com/rabbit/public/_build/results?buildId=626777", "rabbit")]
        [InlineData("https://frog.visualstudio.com/public/_build", "frog")]
        public void GetOrganizationTests(string url, string organization)
        {
            var build = new Build()
            {
                Url = url,
            };
            Assert.Equal(organization, DevOpsUtil.GetOrganization(build));
        }

        [Theory]
        [InlineData(818538, "release/dev16.9-preview1-vs-deps")]
        [InlineData(818474, "master")]
        [InlineData(818436, "master")]
        [InlineData(818471, "master")]
        [InlineData(818398, "master")]
        [InlineData(818403, "master-vs-deps")]
        public void GetTargetBranch(int buildNumber, string targetBranch)
        {
            var json = ResourceUtil.GetJsonFile("build-list2.json");
            var builds = AzureJsonUtil.GetArray<Build>(json);
            var build = builds.Single(x => x.Id == buildNumber);
            Assert.Equal(targetBranch, DevOpsUtil.GetTargetBranch(build));
        }
    }
}
