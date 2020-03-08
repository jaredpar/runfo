using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Query.Test
{
    internal static class ResourceUtil
    {
        internal static Stream GetJsonFileStream(string fileName)
        {
            var fullName = $"Query.Test.JsonData._5._0.{fileName}";
            var assembly = typeof(ResourceUtil).Assembly;
            return assembly.GetManifestResourceStream(fullName);
        }

        internal static string GetJsonFile(string fileName)
        {
            var reader = new StreamReader(GetJsonFileStream(fileName));
            return reader.ReadToEnd();
        }
    }
}
