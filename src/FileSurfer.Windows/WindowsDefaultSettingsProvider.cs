using System.Collections.Generic;

namespace FileSurfer.Core;

public class WindowsDefaultSettingsProvider : IDefaultSettingsProvider
{
    private const string ThisPCLabel = "This PC";

    public SettingsRecord GetDefaultSettings() =>
        new()
        {
            newImageName = "New Image",
            newFileName = "New File",
            newDirectoryName = "New Folder",
            thisPCLabel = ThisPCLabel,
            notepadApp = "notepad.exe",
            openInLastLocation = true,
            openIn = ThisPCLabel,
            useDarkMode = true,
            displayMode = DisplayMode.ListView.ToString(),
            defaultSort = SortBy.Name.ToString(),
            fileSizeUnitLimit = 4096,
            sortReversed = false,
            showSpecialFolders = true,
            showProtectedFiles = false,
            showHiddenFiles = true,
            treatDotFilesAsHidden = true,
            gitIntegration = true,
            showUndoRedoErrorDialogs = true,
            automaticRefresh = true,
            automaticRefreshInterval = 3000,
            allowImagePastingFromClipboard = true,
            quickAccess = new List<string>(),
        };
}
