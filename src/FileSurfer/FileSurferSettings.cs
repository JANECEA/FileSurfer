using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FileSurfer;

public enum DisplayModeEnum
{
    ListView,
    IconView,
}

public enum SortBy
{
    Name,
    Date,
    Type,
    Size,
}

static class FileSurferSettings
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "JSON naming convention")]
    internal record SettingsRecord(
        bool useDarkMode,
        bool openInLastLocation,
        string openIn,
        int fileSizeDisplayLimit,
        string displayMode,
        string defaultSort,
        bool sortReversed,
        bool showSpecialFolders,
        bool showProtectedFiles,
        bool showHiddenFiles,
        bool treatDotFilesAsHidden,
        bool gitIntegration,
        bool showUndoRedoErrorDialogs,
        bool allowImagePastingFromClipboard,
        string newImageName,
        string newFileName,
        string newDirectoryName,
        string thisPCLabel,
        List<string> quickAccess
    );

    private static readonly JsonSerializerOptions serializerOptions =
        new() { WriteIndented = true };
    private static readonly string _settingsFileDir =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FileSurfer";
    public static readonly string SettingsFilePath = _settingsFileDir + "\\settings.json";
    private static string _previousSettingsjson = string.Empty;

    public static bool UseDarkMode { get; set; } = true;
    public static bool OpenInLastLocation { get; set; } = true;
    public static int FileSizeDisplayLimit { get; set; } = 4096;
    public static DisplayModeEnum DisplayMode { get; set; } = DisplayModeEnum.ListView;
    public static SortBy DefaultSort { get; set; } = SortBy.Name;
    public static bool SortReversed { get; set; } = false;
    public static bool ShowSpecialFolders { get; set; } = true;
    public static bool ShowProtectedFiles { get; set; } = false;
    public static bool ShowHiddenFiles { get; set; } = true;
    public static bool TreatDotFilesAsHidden { get; set; } = true;
    public static bool GitIntegration { get; set; } = true;
    public static bool ShowUndoRedoErrorDialogs { get; set; } = true;
    public static bool AllowImagePastingFromClipboard { get; set; } = true;
    public static string NewImageName { get; set; } = "New Image";
    public static string NewFileName { get; set; } = "New File";
    public static string NewDirectoryName { get; set; } = "New Folder";
    public static string ThisPCLabel { get; set; } = "This PC";
    public static string OpenIn { get; set; } = ThisPCLabel;
    public static List<string> QuickAccess { get; set; } = new List<string>();

    public static void LoadSettings()
    {
        if (!File.Exists(SettingsFilePath))
            SaveSettings();

        _previousSettingsjson = File.ReadAllText(SettingsFilePath);

        try
        {
            SettingsRecord settings =
                JsonSerializer.Deserialize<SettingsRecord>(_previousSettingsjson)
                ?? throw new NullReferenceException();

            UseDarkMode = settings.useDarkMode;
            OpenInLastLocation = settings.openInLastLocation;
            OpenIn = settings.openIn;
            FileSizeDisplayLimit = settings.fileSizeDisplayLimit;
            DisplayMode = (DisplayModeEnum)
                Enum.Parse(typeof(DisplayModeEnum), settings.displayMode);
            DefaultSort = (SortBy)Enum.Parse(typeof(SortBy), settings.defaultSort);
            SortReversed = settings.sortReversed;
            ShowSpecialFolders = settings.showSpecialFolders;
            ShowProtectedFiles = settings.showProtectedFiles;
            ShowHiddenFiles = settings.showHiddenFiles;
            TreatDotFilesAsHidden = settings.treatDotFilesAsHidden;
            GitIntegration = settings.gitIntegration;
            ShowUndoRedoErrorDialogs = settings.showUndoRedoErrorDialogs;
            AllowImagePastingFromClipboard = settings.allowImagePastingFromClipboard;
            NewImageName = settings.newImageName;
            NewFileName = settings.newFileName;
            NewDirectoryName = settings.newDirectoryName;
            ThisPCLabel = settings.thisPCLabel;
            QuickAccess = settings.quickAccess;
        }
        catch
        {
            SaveSettings();
        }
    }

    public static void SaveSettings()
    {
        SettingsRecord settings =
            new(
                UseDarkMode,
                OpenInLastLocation,
                OpenIn,
                FileSizeDisplayLimit,
                DisplayMode.ToString(),
                DefaultSort.ToString(),
                SortReversed,
                ShowSpecialFolders,
                ShowProtectedFiles,
                ShowHiddenFiles,
                TreatDotFilesAsHidden,
                GitIntegration,
                ShowUndoRedoErrorDialogs,
                AllowImagePastingFromClipboard,
                NewImageName,
                NewFileName,
                NewDirectoryName,
                ThisPCLabel,
                QuickAccess
            );
        string settingsJson = JsonSerializer.Serialize(settings, serializerOptions);

        if (!Directory.Exists(_settingsFileDir))
            Directory.CreateDirectory(_settingsFileDir);

        if (_previousSettingsjson != settingsJson)
            File.WriteAllText(SettingsFilePath, settingsJson);
    }
}
