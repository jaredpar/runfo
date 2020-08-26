using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace DevOps.Util.UnitTests
{
    internal static class Extensions
    {
        internal static string TrimNewlines(this string str) => str.Trim('\r', '\n');
    }
}
