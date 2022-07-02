using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Runfo;

internal partial class RuntimeInfo
{
    public async Task<int> RunSettingsAsync()
    {
        Console.WriteLine(SettingsData.SettingsFilePath);

        if (!File.Exists(SettingsData.SettingsFilePath))
        {
            await File.WriteAllTextAsync(SettingsData.SettingsFilePath, SettingsData.SettingsContent);
        }

        Process.Start("notepad.exe", SettingsData.SettingsFilePath);
        return RuntimeInfoUtil.ExitSuccess;
    }
}

