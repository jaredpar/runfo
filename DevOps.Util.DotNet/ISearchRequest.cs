using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet.Triage
{
    public interface ISearchRequest
    {
        string GetQueryString();
        void ParseQueryString(string userQuery);
    }
}
