using DevOps.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft;
using Newtonsoft.Json.Linq;

namespace DevOps.Util.DotNet
{
    public static class Extensions
    {
        public static void AddWithValueNullable<T>(this SqlParameterCollection collection, string parameterName, T? value)
            where T : struct
        {
            var realValue = value is null ? DBNull.Value : (object)value.Value;
            collection.AddWithValue(parameterName, realValue);
        }

        public static async ValueTask EnsureOpenAsync(this SqlConnection sqlConnection)
        {
            if (sqlConnection.State != ConnectionState.Open)
            {
                await sqlConnection.OpenAsync();
            }
        }
    }
}
