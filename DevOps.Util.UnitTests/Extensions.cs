using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DevOps.Util.UnitTests
{
    internal static class Extensions
    {
        internal static string TrimNewlines(this string str) => str.Trim('\r', '\n');

        internal static void Clear<T>(this DbSet<T> dbSet)
            where T : class
        {
            dbSet.RemoveRange(dbSet);
        }
    }
}
