using System.Collections.Generic;
using FileSurfer.Core;

namespace FileSurfer.Linux;

public class LinuxDefaultSettingsProvider : IDefaultSettingsProvider
{
    private const string ThisPCLabel = "This PC";

    public void PopulateDefaults(SettingsRecord settingsRecord)
    {
        settingsRecord.newImageName = "New Image";
        settingsRecord.newFileName = "New File";
        settingsRecord.newDirectoryName = "New Folder";
        settingsRecord.thisPCLabel = ThisPCLabel;
        settingsRecord.notepadApp = "nano";
        settingsRecord.openInLastLocation = true;
        settingsRecord.openIn = ThisPCLabel;
        settingsRecord.useDarkMode = true;
        settingsRecord.displayMode = DisplayMode.ListView.ToString();
        settingsRecord.defaultSort = SortBy.Name.ToString();
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
