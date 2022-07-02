using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Runfo;

internal sealed class SettingsData
{
    internal string? AzureToken { get; set; }
    internal string? HelixToken { get; set; }
    internal string? HelixBaseUri { get; set; }

    internal SettingsData()
    {
    }

    internal static async Task<SettingsData> ReadAsync()
    {
        var settingsData = await ReadFromDiskAsync();
        if (Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN") is string azdoToken)
        {
            settingsData.AzureToken = azdoToken;
        }

        if (Environment.GetEnvironmentVariable("RUNFO_HELIX_TOKEN") is string helixToken)
        {
            settingsData.HelixToken = helixToken;
        }

        return settingsData;
    }

    internal static async Task<SettingsData> ReadFromDiskAsync()
    {
        var settingsData = new SettingsData();
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return settingsData;
            }

            var lines = await File.ReadAllLinesAsync(SettingsFilePath);
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line) || line[0] == ';')
                {
                    continue;
                }

                var parts = line.Split('=', count: 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || string.IsNullOrEmpty(parts[1]))
                {
                    continue;
                }

                switch (parts[0])
                {
                    case "azdo-token":
                        settingsData.AzureToken = parts[1];
                        break;
                    case "helix-token":
                        settingsData.HelixToken = parts[1];
                        break;
                    case "helix-base-uri":
                        settingsData.HelixBaseUri = parts[1];
                        break;
                }
            }
        }
        catch
        {
            
        }

        return settingsData;
    }

    internal static readonly string SettingsFilePath = Path.Combine(RuntimeInfoUtil.RunfoDirectory, "settings.txt");
    internal static readonly string SettingsContent = @"
; The azdo token can be overriden by using %RUNFO_AZURE_TOKEN% or passing 
; --azdo-token to the command line 
;
; Create new tokens here: https://dev.azure.com/dnceng/_usersSettings/tokens
azdo-token=

; The helix token can be overriden by using %RUNFO_HELIX_TOKEN% or passing 
; --helix-token to the command line 
;
; Create new tokens here: https://helix.dot.net/Account/Tokens
helix-token=
helix-base-uri=https://helix.dot.net/
";
}
