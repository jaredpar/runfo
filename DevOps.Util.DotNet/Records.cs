using System;
using System.Collections.Generic;
using System.Text;

namespace DevOps.Util.DotNet
{
    public readonly record struct HelixInfoWorkItem(string JobId, string WorkItemName);
}
