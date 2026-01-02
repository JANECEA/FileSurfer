using System.Collections.Generic;
using FileSurfer.Core;

namespace FileSurfer.Windows;

public class WindowsDefaultSettingsProvider : IDefaultSettingsProvider
{
    private const string ThisPcLabel = "This PC";

    public void PopulateDefaults(SettingsRecord settingsRecord)
    {
        settingsRecord.newImageName = "New Image";
        settingsRecord.newFileName = "New File";
        settingsRecord.newDirectoryName = "New Folder";
        settingsRecord.thisPCLabel = ThisPcLabel;
        settingsRecord.notepadApp = "notepad.exe";
        settingsRecord.notepadAppArgs = string.Empty;
        settingsRecord.terminal = "powershell";
        settingsRecord.terminalArgs = "-NoExit -Command Set-Location";
        settingsRecord.openInLastLocation = true;
        settingsRecord.openIn = ThisPcLabel;
        settingsRecord.useDarkMode = true;
        settingsRecord.displayMode = nameof(DisplayMode.ListView);
        settingsRecord.defaultSort = nameof(SortBy.Name);
        settingsRecord.fileSizeUnitLimit = 4096;
        settingsRecord.sortReversed = false;
        settingsRecord.showSpecialFolders = true;
        settingsRecord.showProtectedFiles = false;
        settingsRecord.showHiddenFiles = true;
        settingsRecord.treatDotFilesAsHidden = true;
        settingsRecord.gitIntegration = true;
        settingsRecord.showUndoRedoErrorDialogs = true;
        settingsRecord.automaticRefresh = true;
        settingsRecord.automaticRefreshInterval = 3000;
        settingsRecord.allowImagePastingFromClipboard = true;
        settingsRecord.quickAccess = new List<string>();
    }
}
