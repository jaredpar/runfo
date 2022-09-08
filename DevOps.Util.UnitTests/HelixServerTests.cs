using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace DevOps.Util.UnitTests;

public sealed class HelixServerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TokenEmpty(string tokenValue)
    {
        var helixServer = new HelixServer(token: tokenValue);
        Assert.Null(helixServer.Token);
        Assert.Null(helixServer.HelixApi.Options.Credentials);
    }
}
