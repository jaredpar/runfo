
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
    }
}
