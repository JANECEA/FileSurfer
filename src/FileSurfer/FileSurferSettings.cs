using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text.Json;

namespace FileSurfer;

public enum DisplayModeEnum
{
    ListView,
    IconView
}

public enum SortBy
{
    Name,
    Date,
    Type,
    Size
}

internal record SettingsRecord(
    bool UseDarkMode,
    bool OpenInLastLocation,
    string OpenIn,
    int FileSizeDisplayLimit,
    string DisplayMode,
    string DefaultSort,
    bool SortReversed,
    bool ShowSpecialFolders,
    bool ShowProtectedFiles,
    bool ShowHiddenFiles,
    bool TreatDotFilesAsHidden,
    bool GitIntegration,
    bool ShowUndoRedoErrorDialogs,
    bool AllowImagePastingFromClipboard,
    string NewImageName,
    string NewFileName,
    string NewDirectoryName,
    string ThisPCLabel,
    List<string> QuickAccess
);

static class FileSurferSettings
{
    private static readonly JsonSerializerOptions serializerOptions = new() { WriteIndented = true };
    private static readonly string _settingsFileDir =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        + "\\FileSurfer";
    public static readonly string SettingsFilePath = _settingsFileDir + "\\settings.json";
    private static string _previousSettingsjson = string.Empty;

    public static bool UseDarkMode { get; set; } = true;
    public static bool OpenInLastLocation { get; set; } = true;
    public static string OpenIn { get; set; } = "";
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
    public static List<string> QuickAccess { get; set; } = new List<string>();

    public static void LoadSettings()
    {
        if (!File.Exists(SettingsFilePath))
            SaveSettings();

        _previousSettingsjson = File.ReadAllText(SettingsFilePath);
        dynamic settings =
            JsonSerializer.Deserialize<ExpandoObject>(_previousSettingsjson) ?? throw new ArgumentNullException();

        try
        {
            UseDarkMode = Convert.ToBoolean(settings.UseDarkMode.ToString());
            OpenInLastLocation = Convert.ToBoolean(settings.OpenInLastLocation.ToString());
            OpenIn = settings.OpenIn.ToString();
            FileSizeDisplayLimit = Convert.ToInt32(settings.FileSizeDisplayLimit.ToString());
            DisplayMode = (DisplayModeEnum)Enum.Parse(typeof(DisplayModeEnum), settings.DisplayMode.ToString());
            DefaultSort = (SortBy)Enum.Parse(typeof(SortBy), settings.DefaultSort.ToString());
            SortReversed = Convert.ToBoolean(settings.SortReversed.ToString());
            ShowSpecialFolders = Convert.ToBoolean(settings.ShowSpecialFolders.ToString());
            ShowProtectedFiles = Convert.ToBoolean(settings.ShowProtectedFiles.ToString());
            ShowHiddenFiles = Convert.ToBoolean(settings.ShowHiddenFiles.ToString());
            TreatDotFilesAsHidden = Convert.ToBoolean(settings.TreatDotFilesAsHidden.ToString());
            GitIntegration = Convert.ToBoolean(settings.GitIntegration.ToString());
            ShowUndoRedoErrorDialogs = Convert.ToBoolean(settings.ShowUndoRedoErrorDialogs.ToString());
            AllowImagePastingFromClipboard = Convert.ToBoolean(settings.AllowImagePastingFromClipboard.ToString());
            NewImageName = settings.NewImageName.ToString();
            NewFileName = settings.NewFileName.ToString();
            NewDirectoryName = settings.NewDirectoryName.ToString();
            ThisPCLabel = settings.ThisPCLabel.ToString();
            QuickAccess = JsonSerializer.Deserialize<List<string>>(settings.QuickAccess.ToString());
        }
        catch
        {
            SaveSettings();
        }
    }

    public static void SaveSettings()
    {
        SettingsRecord settingsObject = new(
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
        string settingsJson = JsonSerializer.Serialize(settingsObject, serializerOptions);
        if (!Directory.Exists(_settingsFileDir))
            Directory.CreateDirectory(_settingsFileDir);

        if (_previousSettingsjson != settingsJson)
            File.WriteAllText(SettingsFilePath, settingsJson);
    }
}
