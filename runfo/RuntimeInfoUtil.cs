using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Mono.Options;
using YamlDotNet.Serialization.TypeResolvers;

namespace Runfo
{
    internal static class RuntimeInfoUtil
    {
        internal const int ExitSuccess = 0;
        internal const int ExitFailure = 1;

        internal static readonly string RunfoDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "runfo");
        internal static readonly string CacheDirectory = Path.Combine(RunfoDirectory, "json");

        internal static TimeSpan? TryGetDuration(string startTime, string finishTime)
        {
            if (startTime is null ||
                finishTime is null ||
                !DateTime.TryParse(startTime, out var s) ||
                !DateTime.TryParse(finishTime, out var f))
            {
                return null;
            }

            return f - s;
        }

        internal static async Task<List<T>> ToListAsync<T>(IEnumerable<Task<T>> e)
        {
            await Task.WhenAll(e);
            return e.Select(x => x.Result).ToList();
        }
    }
}