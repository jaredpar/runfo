using System.Collections.Generic;
using System.Linq;
using Mono.Options;

namespace Runfo
{
    internal sealed class GetFromHelixOptionSet : OptionSet
    {
        internal string? JobId { get; set; }

        internal List<string> WorkItems { get; set; } = new List<string>();

        internal string? DownloadDir { get; set; }

        internal string? Token { get; set; }

        internal GetFromHelixOptionSet()
        {
            Add("j|jobid=", "helix job id to download items from.", j => JobId = j);
            Add("o|output=", "output directory to download to.", d => DownloadDir = d);
            Add("t|token=", "Helix authentication token in order to get payload from an internal job.", t => Token = t);
            Add("w|workitems=", "comma separated list of workitems to download.\nAccepted values:\nempty: download only correlation payload.\nlist: separated by comma.\nall: download all workitems.", w => WorkItems = w.Split(",").ToList());
        }
    }
}