using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "IDE1006:Naming Styles",
        Justification = "JSON naming convention"
    )]
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
        bool automaticRefresh,
        int automaticRefreshInterval,
        bool allowImagePastingFromClipboard,
        string newImageName,
        string newFileName,
        string newDirectoryName,
        string thisPCLabel,
        string notepadApp,
        List<string> quickAccess
    );

    private static readonly JsonSerializerOptions serializerOptions =
        new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        };
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
    public static bool AutomaticRefresh { get; set; } = true;
    public static int AutomaticRefreshInterval { get; set; } = 3000;
    public static bool AllowImagePastingFromClipboard { get; set; } = true;
    public static string NewImageName { get; set; } = "New Image";
    public static string NewFileName { get; set; } = "New File";
    public static string NewDirectoryName { get; set; } = "New Folder";
    public static string ThisPCLabel { get; set; } = "This PC";
    public static string NotePadApp { get; set; } = "notepad.exe";
    public static string OpenIn { get; set; } = ThisPCLabel;
    public static List<string> QuickAccess { get; set; } = new List<string>();

    public static void LoadSettings()
    {
        if (!File.Exists(SettingsFilePath))
            SaveSettings();

        _previousSettingsjson = File.ReadAllText(SettingsFilePath, Encoding.UTF8);

        try
        {
            SettingsRecord settings =
                JsonSerializer.Deserialize<SettingsRecord>(_previousSettingsjson)
                ?? throw new NullReferenceException();

            UseDarkMode = settings.useDarkMode;
            OpenInLastLocation = settings.openInLastLocation;
            OpenIn = settings.openIn;
            if (settings.fileSizeDisplayLimit > 0)
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
            AutomaticRefresh = settings.automaticRefresh;
            if (settings.automaticRefreshInterval > 0)
                AutomaticRefreshInterval = settings.automaticRefreshInterval;

            AllowImagePastingFromClipboard = settings.allowImagePastingFromClipboard;
            if (!string.IsNullOrWhiteSpace(settings.newImageName))
                NewImageName = settings.newImageName;

            if (!string.IsNullOrWhiteSpace(settings.newFileName))
                NewFileName = settings.newFileName;

            if (!string.IsNullOrWhiteSpace(settings.newDirectoryName))
                NewDirectoryName = settings.newDirectoryName;

            if (!string.IsNullOrWhiteSpace(settings.thisPCLabel))
                ThisPCLabel = settings.thisPCLabel;

            NotePadApp = settings.notepadApp;
            QuickAccess = settings.quickAccess;
        }
        catch
        {
            SaveSettings();
        }
    }

    public static void UpdateQuickAccess(IEnumerable<FileSystemEntry> quickAccess) =>
        QuickAccess = quickAccess.Select(entry => entry.PathToEntry).ToList();

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
                AutomaticRefresh,
                AutomaticRefreshInterval,
                AllowImagePastingFromClipboard,
                NewImageName,
                NewFileName,
                NewDirectoryName,
                ThisPCLabel,
                NotePadApp,
                QuickAccess
            );
        string settingsJson = JsonSerializer.Serialize(settings, serializerOptions);

        if (!Directory.Exists(_settingsFileDir))
            Directory.CreateDirectory(_settingsFileDir);

        if (_previousSettingsjson != settingsJson)
            File.WriteAllText(SettingsFilePath, settingsJson, Encoding.UTF8);
    }
}
