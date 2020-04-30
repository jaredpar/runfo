#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet
{
    public static class Extensions
    {
        public static IEnumerable<T> SelectNullableValue<T>(this IEnumerable<T?> enumerable)
            where T : struct
        {
            foreach (var current in enumerable)
            {
                if (current.HasValue)
                {
                    yield return current.Value;
                }
            }
        }

        public static IEnumerable<U> SelectNullableValue<T, U>(this IEnumerable<T> enumerable, Func<T, U?> func)
            where U : struct =>
            enumerable
                .Select(func)
                .SelectNullableValue();

        public static async Task<T?> FirstOrDefaultAsync<T>(this IAsyncEnumerable<T> enumerable)
            where T : class
        {
            await foreach (var current in enumerable.ConfigureAwait(false))
            {
                return current;
            }

            return default;
        }
    }
}
