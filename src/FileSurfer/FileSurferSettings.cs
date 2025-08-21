using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileSurfer.ViewModels;

namespace FileSurfer;

/// <summary>
/// Defines the display modes available in FileSurfer for viewing <see cref="FileSystemEntryViewModel"/>s.
/// </summary>
public enum DisplayMode
{
    /// <summary>
    /// Displays <see cref="FileSystemEntryViewModel"/>s as a list.
    /// </summary>
    ListView,

    /// <summary>
    /// Displays <see cref="FileSystemEntryViewModel"/>s in a grid with larger icons.
    /// </summary>
    IconView,
}

/// <summary>
/// Specifies the order by which <see cref="FileSystemEntryViewModel"/>s can be sorted in the FileSurfer application.
/// </summary>
public enum SortBy
{
    /// <summary>
    /// Sorts <see cref="MainWindowViewModel.FileEntries"/> by <see cref="FileSystemEntryViewModel.Name"/>.
    /// </summary>
    Name,

    /// <summary>
    /// Sorts <see cref="MainWindowViewModel.FileEntries"/> by <see cref="FileSystemEntryViewModel.LastModTime"/>.
    /// </summary>
    Date,

    /// <summary>
    /// Sorts <see cref="MainWindowViewModel.FileEntries"/> by <see cref="FileSystemEntryViewModel.Type"/>.
    /// </summary>
    Type,

    /// <summary>
    /// Sorts <see cref="MainWindowViewModel.FileEntries"/> by <see cref="FileSystemEntryViewModel.SizeB"/>.
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
public record SettingsRecord
{
    private const string ThisPCLabel = "This PC";
    public string newImageName { get; init; } = "New Image";
    public string newFileName { get; init; } = "New File";
    public string newDirectoryName { get; init; } = "New Folder";
    public string thisPCLabel { get; init; } = ThisPCLabel;
    public string notepadApp { get; init; } = "notepad.exe";
    public bool openInLastLocation { get; init; } = true;
    public string openIn { get; init; } = ThisPCLabel;
    public bool useDarkMode { get; init; } = true;
    public string displayMode { get; init; } = DisplayMode.ListView.ToString();
    public string defaultSort { get; init; } = SortBy.Name.ToString();
    public int fileSizeUnitLimit { get; init; } = 4096;
    public bool sortReversed { get; init; } = false;
    public bool showSpecialFolders { get; init; } = true;
    public bool showProtectedFiles { get; init; } = false;
    public bool showHiddenFiles { get; init; } = true;
    public bool treatDotFilesAsHidden { get; init; } = true;
    public bool gitIntegration { get; init; } = true;
    public bool showUndoRedoErrorDialogs { get; init; } = true;
    public bool automaticRefresh { get; init; } = true;
    public int automaticRefreshInterval { get; init; } = 3000;
    public bool allowImagePastingFromClipboard { get; init; } = true;
    public List<string> quickAccess { get; init; } = new List<string>();
}

/// <summary>
/// <para>
/// Provides application-wide settings management for the FileSurfer application.
/// </para>
/// Handles the loading, saving, and updating of user preferences and settings.
/// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
internal static class FileSurferSettings
{
    public const long ShowDialogLimitB = 250 * 1024 * 1024; // 250 MiB
    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            AllowTrailingCommas = true,
        };
    private static readonly string SettingsFileDir =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FileSurfer";

    /// <summary>
    /// Returns the default set of settings for the <see cref="FileSurfer"/> app.
    /// </summary>
    public static SettingsRecord DefaultSettings => new();

    /// <summary>
    /// Returns the current settings in the form of <see cref="SettingsRecord"/>.
    /// </summary>
    public static SettingsRecord CurrentSettings =>
        new SettingsRecord
        {
            newImageName = NewImageName,
            newFileName = NewFileName,
            newDirectoryName = NewDirectoryName,
            thisPCLabel = ThisPCLabel,
            notepadApp = NotepadApp,
            openInLastLocation = OpenInLastLocation,
            openIn = OpenIn,
            useDarkMode = UseDarkMode,
            displayMode = DisplayMode.ToString(),
            defaultSort = DefaultSort.ToString(),
            fileSizeUnitLimit = FileSizeUnitLimit,
            sortReversed = SortReversed,
            showSpecialFolders = ShowSpecialFolders,
            showProtectedFiles = ShowProtectedFiles,
            showHiddenFiles = ShowHiddenFiles,
            treatDotFilesAsHidden = TreatDotFilesAsHidden,
            gitIntegration = GitIntegration,
            showUndoRedoErrorDialogs = ShowUndoRedoErrorDialogs,
            automaticRefresh = AutomaticRefresh,
            automaticRefreshInterval = AutomaticRefreshInterval,
            allowImagePastingFromClipboard = AllowImagePastingFromClipboard,
            quickAccess = QuickAccess,
        };

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
    /// Defines the view mode for displaying files and folders. Defaults to <see cref="DisplayMode.ListView"/>.
    /// </summary>
    public static DisplayMode DisplayMode { get; set; }

    /// <summary>
    /// Specifies the default sorting method for files and folders. Defaults to sorting by <see cref="SortBy.Name"/>.
    /// </summary>
    public static SortBy DefaultSort { get; set; }

    /// <summary>
    /// Numerical limit before FileSurfer uses the next byte unit. Defaults to <c>4096</c>.
    /// </summary>
    public static int FileSizeUnitLimit { get; set; }

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
                JsonSerializer.Deserialize<SettingsRecord>(_previousSettingsJson, SerializerOptions)
                ?? throw new InvalidDataException();

            ImportSettings(settings);
        }
        catch
        {
            ImportSettings(DefaultSettings);
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
        if (settings.fileSizeUnitLimit > 0)
            FileSizeUnitLimit = settings.fileSizeUnitLimit;

        DisplayMode = Enum.Parse<DisplayMode>(settings.displayMode);
        DefaultSort = Enum.Parse<SortBy>(settings.defaultSort);
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
    /// Update Quick Access list with the specified <see cref="FileSystemEntryViewModel"/>s.
    /// </summary>
    /// <param name="quickAccess"></param>
    public static void UpdateQuickAccess(IEnumerable<FileSystemEntryViewModel> quickAccess) =>
        QuickAccess = quickAccess.Select(entry => entry.PathToEntry).ToList();

    /// <summary>
    /// Saves the current settings to the settings file if any changes have been made.
    /// </summary>
    public static void SaveSettings()
    {
        SettingsRecord settings = CurrentSettings;
        string settingsJson = JsonSerializer.Serialize(settings, SerializerOptions);

        if (!Directory.Exists(SettingsFileDir))
            Directory.CreateDirectory(SettingsFileDir);

        if (_previousSettingsJson != settingsJson)
            File.WriteAllText(SettingsFilePath, settingsJson, Encoding.UTF8);
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
