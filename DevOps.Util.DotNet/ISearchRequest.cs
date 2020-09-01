using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.Triage
{
    public interface ISearchRequest
    {
        string GetQueryString();
        void ParseQueryString(string userQuery);
    }
}
