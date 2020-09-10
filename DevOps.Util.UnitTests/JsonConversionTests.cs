using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public class JsonConversionTests
    {
        public sealed class Build
        {
            [Fact]
            public void List1()
            {
                var json = ResourceUtil.GetJsonFile("build-list1.json");
                var root = JObject.Parse(json);
                var array = (JArray)root["value"];
                var buildArray = array.ToObject<Build[]>();
                Assert.Single(buildArray);
            }
        }
    }
}
