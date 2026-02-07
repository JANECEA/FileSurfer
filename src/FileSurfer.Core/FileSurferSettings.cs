using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileSurfer.Core.Models.Sftp;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core;

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
/// <para>
/// Provides application-wide settings management for the FileSurfer application.
/// </para>
/// Handles the loading, saving, and updating of user preferences and settings.
/// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
[SuppressMessage(
    "ReSharper",
    "MemberCanBePrivate.Global",
    Justification = "Members are part of the settings for the app"
)]
public static class FileSurferSettings
{
    public const long ShowDialogLimitB = 250 * 1024 * 1024; // 250 MiB
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();
    private static readonly string FileSurferDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FileSurfer"
    );

    /// <summary>
    /// Executes this function when settings are updated in <see cref="ImportSettings"/>.
    /// </summary>
    public static Action? OnSettingsChange { private get; set; }

    /// <summary>
    /// Returns the default set of settings for the <see cref="FileSurfer"/> app.
    /// </summary>
    public static SettingsRecord DefaultSettings => new();

    /// <summary>
    /// Returns the current settings in the form of <see cref="SettingsRecord"/>.
    /// </summary>
    public static SettingsRecord CurrentSettings =>
        new()
        {
            newImageName = NewImageName,
            newFileName = NewFileName,
            newDirectoryName = NewDirectoryName,
            thisPCLabel = ThisPcLabel,
            notepadApp = NotepadApp,
            notepadAppArgs = NotepadAppArgs,
            terminal = Terminal,
            terminalArgs = TerminalArgs,
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
    public static readonly string SettingsFilePath = Path.Combine(
        FileSurferDataDir,
        "settings.json"
    );

    /// <summary>
    /// The full path to sftp.json.
    /// </summary>
    public static readonly string SftpConnectionsFilePath = Path.Combine(
        FileSurferDataDir,
        "sftp.json"
    );

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
    public static string ThisPcLabel { get; set; }

    /// <summary>
    /// The application, the 'Open in Notepad' context menu option will open.
    /// </summary>
    public static string NotepadApp { get; set; }

    /// <summary>
    /// The args for <see cref="NotepadApp"/>
    /// </summary>
    public static string NotepadAppArgs { get; set; }

    /// <summary>
    /// The preferred terminal app
    /// </summary>
    public static string Terminal { get; set; }

    /// <summary>
    /// The args for <see cref="Terminal"/>
    /// </summary>
    public static string TerminalArgs { get; set; }

    /// <summary>
    /// Specifies if the app should reopen files or folders in their last accessed location. Defaults to <see langword="true"/>.
    /// </summary>
    public static bool OpenInLastLocation { get; set; }

    /// <summary>
    /// Specifies the default location where FileSurfer opens. Defaults to the value of <see cref="ThisPcLabel"/>.
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
    internal const int FileSizeUnitLimitLowerBound = 512;
    internal const int FileSizeUnitLimitUpperBound = 9999;

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
    internal const int AutomaticRefreshIntervalLowerBound = 100;
    internal const int AutomaticRefreshIntervalUpperBound = 60 * 1000;

    /// <summary>
    /// Specifies if images stored in the system clipboard can be pasted directly into directories. Defaults to <see langword="true"/>.
    /// </summary>
    public static bool AllowImagePastingFromClipboard { get; set; }

    /// <summary>
    /// List of directories and files added by the user for quick access. Defaults to an empty list.
    /// </summary>
    public static List<string> QuickAccess { get; set; }

    /// <summary>
    /// List of SFTP connections defined by the user. Defaults to an empty list.
    /// </summary>
    public static List<SftpConnection> SftpConnections { get; set; }

    /// <summary>
    /// <para>
    /// Loads settings from the settings file and applies them to the current session.
    /// </para>
    /// If the settings file does not exist or is invalid, default settings are used and settings.json is rewritten.
    /// </summary>
    public static void Initialize(IDefaultSettingsProvider settingsProvider)
    {
        try
        {
            string sftpConnections = File.ReadAllText(SftpConnectionsFilePath, Encoding.UTF8);
            SftpConnections =
                JsonSerializer.Deserialize<List<SftpConnection>>(
                    sftpConnections,
                    SftpConnection.SerializerOptions
                ) ?? throw new InvalidDataException();
        }
        catch
        {
            SftpConnections = new List<SftpConnection>();
        }

        SettingsRecord.Initialize(settingsProvider);
        try
        {
            _previousSettingsJson = File.ReadAllText(SettingsFilePath, Encoding.UTF8);
            SettingsRecord settings =
                JsonSerializer.Deserialize<SettingsRecord>(
                    _previousSettingsJson,
                    SettingsRecord.SerializerOptions
                ) ?? throw new InvalidDataException();

            ImportSettings(settings);
        }
        catch
        {
            ImportSettings(DefaultSettings);
        }
    }

    /// <summary>
    /// Loads and sanitizes settings from the <see cref="SettingsRecord"/> object and applies them to the current session.
    /// </summary>
    public static void ImportSettings(SettingsRecord settings)
    {
        SettingsRecord defaultSettings = DefaultSettings;

        NewImageName = SanitizeName(
            settings.newImageName,
            InvalidFileNameChars,
            defaultSettings.newImageName
        );
        NewFileName = SanitizeName(
            settings.newFileName,
            InvalidFileNameChars,
            defaultSettings.newFileName
        );
        NewDirectoryName = SanitizeName(
            settings.newDirectoryName,
            InvalidFileNameChars,
            defaultSettings.newDirectoryName
        );
        ThisPcLabel = SanitizeName(
            settings.thisPCLabel,
            InvalidFileNameChars,
            defaultSettings.thisPCLabel
        );
        NotepadApp = SanitizeName(
            settings.notepadApp,
            InvalidPathChars,
            defaultSettings.notepadApp
        );
        NotepadAppArgs = settings.notepadAppArgs.Trim();
        Terminal = SanitizeName(settings.terminal, InvalidPathChars, defaultSettings.terminal);
        TerminalArgs = settings.terminalArgs.Trim();

        OpenInLastLocation = settings.openInLastLocation;
        OpenIn = SanitizeName(settings.openIn, InvalidPathChars, defaultSettings.openIn);
        UseDarkMode = settings.useDarkMode;
        DisplayMode = SafeParseEnum<DisplayMode>(settings.displayMode);
        DefaultSort = SafeParseEnum<SortBy>(settings.defaultSort);

        FileSizeUnitLimit = ClampValue(
            settings.fileSizeUnitLimit,
            FileSizeUnitLimitLowerBound,
            FileSizeUnitLimitUpperBound
        );

        SortReversed = settings.sortReversed;
        ShowSpecialFolders = settings.showSpecialFolders;
        ShowProtectedFiles = settings.showProtectedFiles;
        ShowHiddenFiles = settings.showHiddenFiles;
        TreatDotFilesAsHidden = settings.treatDotFilesAsHidden;
        GitIntegration = settings.gitIntegration;
        ShowUndoRedoErrorDialogs = settings.showUndoRedoErrorDialogs;
        AutomaticRefresh = settings.automaticRefresh;

        AutomaticRefreshInterval = ClampValue(
            settings.automaticRefreshInterval,
            AutomaticRefreshIntervalLowerBound,
            AutomaticRefreshIntervalUpperBound
        );

        AllowImagePastingFromClipboard = settings.allowImagePastingFromClipboard;
        QuickAccess = settings.quickAccess ?? new List<string>();

        OnSettingsChange?.Invoke();
    }

    private static TEnum SafeParseEnum<TEnum>(string? enumValueName)
        where TEnum : struct, Enum =>
        enumValueName is not null && Enum.TryParse(enumValueName, true, out TEnum result)
            ? result
            : default;

    private static string SanitizeName(string? fileName, char[] invalidChars, string defaultName) =>
        fileName is not null
        && fileName.Length > 0
        && fileName.All(ch => !invalidChars.Contains(ch))
            ? fileName
            : defaultName;

    private static T ClampValue<T>(T value, T lowerBound, T upperBound)
        where T : IComparable<T>
    {
        if (value.CompareTo(lowerBound) < 0)
            return lowerBound;

        if (value.CompareTo(upperBound) > 0)
            return upperBound;

        return value;
    }

    /// <summary>
    /// Saves the current settings to the settings file if any changes have been made.
    /// </summary>
    public static void SaveSettings()
    {
        if (!Directory.Exists(FileSurferDataDir))
            Directory.CreateDirectory(FileSurferDataDir);

        SettingsRecord settings = CurrentSettings;
        string settingsJson = JsonSerializer.Serialize(settings, SettingsRecord.SerializerOptions);

        if (_previousSettingsJson != settingsJson)
            File.WriteAllText(SettingsFilePath, settingsJson, Encoding.UTF8);

        string sftpConnectionsJson = JsonSerializer.Serialize(
            SftpConnections,
            SftpConnection.SerializerOptions
        );
        File.WriteAllText(SftpConnectionsFilePath, sftpConnectionsJson, Encoding.UTF8);
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
