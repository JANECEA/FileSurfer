using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FileSurfer;

/// <summary>
/// Defines the display modes available in FileSurfer for viewing <see cref="FileSystemEntry"/>s.
/// </summary>
public enum DisplayModeEnum
{
    /// <summary>
    /// Displays <see cref="FileSystemEntry"/>s as a list.
    /// </summary>
    ListView,

    /// <summary>
    /// Displays <see cref="FileSystemEntry"/>s in a grid with larger icons.
    /// </summary>
    IconView,
}

/// <summary>
/// Specifies the order by which <see cref="FileSystemEntry"/>s can be sorted in the FileSurfer application.
/// </summary>
public enum SortBy
{
    /// <summary>
    /// Sorts <see cref="ViewModels.MainWindowViewModel.FileEntries"/> by <see cref="FileSystemEntry.Name"/>.
    /// </summary>
    Name,

    /// <summary>
    /// Sorts <see cref="ViewModels.MainWindowViewModel.FileEntries"/> by <see cref="FileSystemEntry.LastModTime"/>.
    /// </summary>
    Date,

    /// <summary>
    /// Sorts <see cref="ViewModels.MainWindowViewModel.FileEntries"/> by <see cref="FileSystemEntry.Type"/>.
    /// </summary>
    Type,

    /// <summary>
    /// Sorts <see cref="ViewModels.MainWindowViewModel.FileEntries"/> by <see cref="FileSystemEntry.SizeB"/>.
    /// </summary>
    Size,
}

/// <summary>
/// Used to (de)serialize the settings.json file.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Style",
    "IDE1006:Naming Styles",
    Justification = "JSON naming convention"
)]
public record SettingsRecord(
    string newImageName,
    string newFileName,
    string newDirectoryName,
    string thisPCLabel,
    string notepadApp,
    bool openInLastLocation,
    string openIn,
    bool useDarkMode,
    string displayMode,
    string defaultSort,
    int fileSizeDisplayLimit,
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
    List<string> quickAccess
);

/// <summary>
/// <para>
/// Provides application-wide settings management for the FileSurfer application.
/// </para>
/// Handles the loading, saving, and updating of user preferences and settings.
/// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
static class FileSurferSettings
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        };
    private static readonly string SettingsFileDir =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FileSurfer";

    /// <summary>
    /// The full path to settings.json.
    /// </summary>
    public static readonly string SettingsFilePath = SettingsFileDir + "\\settings.json";
    private static string _previousSettingsJson = string.Empty;

    /// <summary>
    /// Default name for the pasted image files. Defaults to <c>"New Image"</c>.
    /// </summary>
    public static string NewImageName { get; set; }

    /// <summary>
    /// Default name for newly created files. Defaults to <c>"New File"</c>.
    /// </summary>
    public static string NewFileName { get; set; }

    /// <summary>
    /// Default name for newly created directories. Defaults to <c>"New Folder"</c>.
    /// </summary>
    public static string NewDirectoryName { get; set; }

    /// <summary>
    /// What "This PC" 'directory' will be called. Defaults to <c>"This PC"</c>.
    /// </summary>
    public static string ThisPCLabel { get; set; }

    /// <summary>
    /// The application, the 'Open in Notepad' context menu option will open. Defaults to <c>"notepad.exe"</c>.
    /// </summary>
    public static string NotepadApp { get; set; }

    /// <summary>
    /// Specifies if the app should reopen files or folders in their last accessed location. Defaults to <see langword="true"/>.
    /// </summary>
    public static bool OpenInLastLocation { get; set; }

    /// <summary>
    /// Specifies the default location where FileSurfer opens. Defaults to the value of <see cref="ThisPCLabel"/>.
    /// </summary>
    public static string OpenIn { get; set; }

    /// <summary>
    /// Indicates whether the application uses a dark theme. Defaults to <see langword="true"/>.
    /// </summary>
    public static bool UseDarkMode { get; set; }

    /// <summary>
    /// Defines the view mode for displaying files and folders. Defaults to <see cref="DisplayModeEnum.ListView"/>.
    /// </summary>
    public static DisplayModeEnum DisplayMode { get; set; }

    /// <summary>
    /// Specifies the default sorting method for files and folders. Defaults to sorting by <see cref="SortBy.Name"/>.
    /// </summary>
    public static SortBy DefaultSort { get; set; }

    /// <summary>
    /// Numerical limit before FileSurfer uses the next byte unit. Defaults to <c>4096</c>.
    /// </summary>
    public static int FileSizeDisplayLimit { get; set; }

    /// <summary>
    /// Indicates whether file and folder sorting should be reversed. Defaults to <see langword="false"/>.
    /// </summary>
    public static bool SortReversed { get; set; }

    /// <summary>
    /// Determines if special folders (like "Documents" or "Downloads") should be displayed in the sidebar. Defaults to <see langword="true"/>.
    /// </summary>
    public static bool ShowSpecialFolders { get; set; }

    /// <summary>
    /// Controls whether files with protection settings are shown in directory contents and searching. Defaults to <see langword="false"/>.
    /// </summary>
    public static bool ShowProtectedFiles { get; set; }

    /// <summary>
    /// Specifies if hidden files are shown in directory contents and searching. Defaults to <see langword="true"/>.
    /// </summary>
    public static bool ShowHiddenFiles { get; set; }

    /// <summary>
    /// Decides if files starting with a dot '.' are considered hidden. Defaults to <see langword="true"/>.
    /// </summary>
    public static bool TreatDotFilesAsHidden { get; set; }

    /// <summary>
    /// Enables or disables Git integration features within the application. Defaults to <see langword="true"/>.
    /// </summary>
    public static bool GitIntegration { get; set; }

    /// <summary>
    /// Determines if error dialogs should be shown for undo/redo operations. Defaults to <see langword="true"/>.
    /// </summary>
    public static bool ShowUndoRedoErrorDialogs { get; set; }

    /// <summary>
    /// Indicates whether the file explorer should automatically refresh at intervals. Defaults to <see langword="true"/>.
    /// </summary>
    public static bool AutomaticRefresh { get; set; }

    /// <summary>
    /// Sets the interval (in milliseconds) for automatic refreshing of the file explorer. Defaults to <c>3000</c> ms (3 seconds).
    /// </summary>
    public static int AutomaticRefreshInterval { get; set; }

    /// <summary>
    /// Specifies if images stored in the system clipboard can be pasted directly into directories. Defaults to <see langword="true"/>.
    /// </summary>
    public static bool AllowImagePastingFromClipboard { get; set; }

    /// <summary>
    /// List of directories and files added by the user for quick access. Defaults to an empty list.
    /// </summary>
    public static List<string> QuickAccess { get; set; }

    /// <summary>
    /// <para>
    /// Loads settings from the settings file and applies them to the current session.
    /// </para>
    /// If the settings file does not exist or is invalid, default settings are used and settings.json is rewritten.
    /// </summary>
    public static void LoadSettings()
    {
        if (!File.Exists(SettingsFilePath))
            SaveSettings();

        _previousSettingsJson = File.ReadAllText(SettingsFilePath, Encoding.UTF8);

        try
        {
            SettingsRecord settings =
                JsonSerializer.Deserialize<SettingsRecord>(_previousSettingsJson)
                ?? throw new NullReferenceException();

            ImportSettings(settings);
        }
        catch
        {
            ImportSettings(GetCurrentSettings());
            SaveSettings();
        }
    }

    /// <summary>
    /// Loads settings from the <see cref="SettingsRecord"/> object and applies them to the current session.
    /// </summary>
    public static void ImportSettings(SettingsRecord settings)
    {
        UseDarkMode = settings.useDarkMode;
        OpenInLastLocation = settings.openInLastLocation;
        OpenIn = settings.openIn;
        if (settings.fileSizeDisplayLimit > 0)
            FileSizeDisplayLimit = settings.fileSizeDisplayLimit;

        DisplayMode = (DisplayModeEnum)Enum.Parse(typeof(DisplayModeEnum), settings.displayMode);
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

        NotepadApp = settings.notepadApp;
        QuickAccess = settings.quickAccess;
    }

    /// <summary>
    /// Update Quick Access list with the specified <see cref="FileSystemEntry"/>s.
    /// </summary>
    /// <param name="quickAccess"></param>
    public static void UpdateQuickAccess(IEnumerable<FileSystemEntry> quickAccess) =>
        QuickAccess = quickAccess.Select(entry => entry.PathToEntry).ToList();

    /// <summary>
    /// Returns the default set of settings for the <see cref="FileSurfer"/> app.
    /// </summary>
    public static SettingsRecord GetDefaultSettings()
    {
        string thisPCLabel = "This PC";
        return new(
            "New Image",
            "New File",
            "New Folder",
            thisPCLabel,
            "notepad.exe",
            true,
            thisPCLabel,
            true,
            DisplayModeEnum.ListView.ToString(),
            SortBy.Name.ToString(),
            4096,
            false,
            true,
            false,
            true,
            true,
            true,
            true,
            true,
            3000,
            true,
            new List<string>()
        );
    }

    /// <summary>
    /// Returns the current settings in the form of <see cref="SettingsRecord"/>.
    /// </summary>
    public static SettingsRecord GetCurrentSettings() =>
        new(
            NewImageName,
            NewFileName,
            NewDirectoryName,
            ThisPCLabel,
            NotepadApp,
            OpenInLastLocation,
            OpenIn,
            UseDarkMode,
            DisplayMode.ToString(),
            DefaultSort.ToString(),
            FileSizeDisplayLimit,
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
            QuickAccess
        );

    /// <summary>
    /// Saves the current settings to the settings file if any changes have been made.
    /// </summary>
    public static void SaveSettings()
    {
        SettingsRecord settings = GetCurrentSettings();
        string settingsJson = JsonSerializer.Serialize(settings, SerializerOptions);

        if (!Directory.Exists(SettingsFileDir))
            Directory.CreateDirectory(SettingsFileDir);

        if (_previousSettingsJson != settingsJson)
            File.WriteAllText(SettingsFilePath, settingsJson, Encoding.UTF8);
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
