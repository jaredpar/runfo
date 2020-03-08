using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace Query.Test
{
    public class JsonConversion
    {
        public sealed class Build
        {
            [Fact]
            public void List1()
            {
                var json = ResourceUtil.GetJsonFile("build-list-1.json");
                var root = JObject.Parse(json);
                var array = (JArray)root["value"];
                var buildArray = array.ToObject<Build[]>();
                Assert.Single(buildArray);
            }
        }

    }
}
