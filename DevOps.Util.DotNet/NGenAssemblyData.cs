using DevOps.Util;
using DevOps.Util.DotNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet
{
    public readonly struct NGenAssemblyData
    {
        public string AssemblyName { get; }
        public string TargetFramework { get; }
        public int MethodCount { get; }

        public NGenAssemblyData(
            string assemblyName,
            string targetFramework,
            int methodCount)
        {
            AssemblyName = assemblyName;
            TargetFramework = targetFramework;
            MethodCount = methodCount;
        }

        public override string ToString() => $"{AssemblyName} - {TargetFramework}";
    }
}
