using System.Collections.Generic;
using FileSurfer.Core;

namespace FileSurfer.Windows;

public class WindowsDefaultSettingsProvider : IDefaultSettingsProvider
{
    public void PopulateDefaults(SettingsRecord settingsRecord)
    {
        settingsRecord.notepadApp = "notepad.exe";
        settingsRecord.notepadAppArgs = string.Empty;
        settingsRecord.terminal = "powershell";
        settingsRecord.terminalArgs = "-NoExit -Command Set-Location";
    }
}
