using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util
{
    internal static class AzureJsonUtil
    {
        internal static T[] GetArray<T>(string json)
        {
            var root = JObject.Parse(json);
            var array = (JArray)root["value"];
            return array.ToObject<T[]>();
        }

        internal static T GetObject<T>(string json) => JsonConvert.DeserializeObject<T>(json);
    }
}