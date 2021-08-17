using DevOps.Util.DotNet;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public sealed class CodeHygieneTests
    {
        /// <summary>
        /// Make sure any temporary flips to use the production key vault in debug don't get checked
        /// into the main code base
        /// </summary>
        [Fact]
        public void KeyVaultStringTest()
        {
            Assert.Contains("runfo-test", DotNetConstants.KeyVaultEndPointTest);
            Assert.Contains("runfo-prod", DotNetConstants.KeyVaultEndPointProduction);
#if DEBUG
            Assert.Equal(DotNetConstants.KeyVaultEndPointTest, DotNetConstants.KeyVaultEndPoint);
#else
            Assert.Equal(DotNetConstants.KeyVaultEndPointProduction, DotNetConstants.KeyVaultEndPoint);
#endif
        }
    }
}
