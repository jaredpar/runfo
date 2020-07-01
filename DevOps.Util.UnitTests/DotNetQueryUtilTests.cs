using DevOps.Util;
using DevOps.Util.DotNet;
using Newtonsoft.Json;
using System;
using System.Linq;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public class DotNetQueryUtilTests
    {
        [Theory]
        [InlineData("-c 5", new[] { "-c", "5" })]
        [InlineData("-c 5 -n \"A B\"", new[] { "-c", "5", "-n", "\"A B\"" })]
        public void TokenizeQuery(string query, string[] tokens)
        {
            Assert.Equal(tokens, DotNetQueryUtil.TokenizeQuery(query));
        }

    }
}
