using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DevOps.Util.UnitTests
{
    internal static class ResourceUtil
    {
        internal static Stream GetJsonFileStream(string fileName)
        {
            var fullName = $"DevOps.Util.UnitTests.JsonData._5._0.{fileName}";
            var assembly = typeof(ResourceUtil).Assembly;
            return assembly.GetManifestResourceStream(fullName) ?? throw new Exception("Could not get resource stream");
        }

        internal static string GetJsonFile(string fileName)
        {
            var reader = new StreamReader(GetJsonFileStream(fileName));
            return reader.ReadToEnd();
        }

        internal static Timeline GetTimeline(string resourceFileName)
        {
            var json = GetJsonFile(resourceFileName);
            return JsonConvert.DeserializeObject<Timeline>(json);
        }

    }
}
