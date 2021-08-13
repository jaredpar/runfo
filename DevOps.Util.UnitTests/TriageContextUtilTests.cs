using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

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
#if DEBUG
            Assert.Equal(DotNetConstants.KeyVaultEndPointTest, DotNetConstants.KeyVaultEndPoint);
#else
            Assert.Equal(DotNetConstants.KeyVaultEndPointProduction, DotNetConstants.KeyVaultEndPoint);
#endif
        }
    }
}
