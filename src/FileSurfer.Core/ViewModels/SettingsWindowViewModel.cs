using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using ReactiveUI;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
#pragma warning disable CA1822 // Members can be made static.

namespace FileSurfer.Core.ViewModels;

/// <summary>
/// The SettingsWindowViewModel is the ViewModel for the <see cref="Views.SettingsWindow"/>.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global")]
public sealed class SettingsWindowViewModel : ReactiveObject
{
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Gets whether the application is currently running on Windows.
    /// </summary>
    public bool IsWindows { get; } = OperatingSystem.IsWindows();

    /// <summary>
    /// Gets or sets the default name used for newly created image files.
    /// </summary>
    public string NewImageName
    {
        get => _newImageName;
        set =>
            this.RaiseAndSetIfChanged(
                ref _newImageName,
                SanitizeInput(value, InvalidFileNameChars)
            );
    }
    private string _newImageName;

    /// <summary>
    /// Gets or sets the default name used for newly created text files.
    /// </summary>
    public string NewTextFileName
    {
        get => _newTextFileName;
        set =>
            this.RaiseAndSetIfChanged(
                ref _newTextFileName,
                SanitizeInput(value, InvalidFileNameChars)
            );
    }
    private string _newTextFileName;

    /// <summary>
    /// Gets or sets the default name used for newly created files.
    /// </summary>
    public string NewFileName
    {
        get => _newFileName;
        set =>
            this.RaiseAndSetIfChanged(ref _newFileName, SanitizeInput(value, InvalidFileNameChars));
    }
    private string _newFileName;

    /// <summary>
    /// Gets or sets the default name used for newly created directories.
    /// </summary>
    public string NewDirectoryName
    {
        get => _newDirectoryName;
        set =>
            this.RaiseAndSetIfChanged(
                ref _newDirectoryName,
                SanitizeInput(value, InvalidFileNameChars)
            );
    }
    private string _newDirectoryName;

    /// <summary>
    /// Gets or sets the configured text editor executable path.
    /// </summary>
    public string NotepadApp
    {
        get => _notepadApp;
        set => this.RaiseAndSetIfChanged(ref _notepadApp, SanitizeInput(value, InvalidPathChars));
    }
    private string _notepadApp;

    /// <summary>
    /// Gets or sets command-line arguments for the configured text editor.
    /// </summary>
    public string NotepadAppArgs
    {
        get => _notepadAppArgs;
        set => this.RaiseAndSetIfChanged(ref _notepadAppArgs, value);
    }
    private string _notepadAppArgs;

    /// <summary>
    /// Gets or sets the configured terminal executable path.
    /// </summary>
    public string Terminal
    {
        get => _terminal;
        set => this.RaiseAndSetIfChanged(ref _terminal, SanitizeInput(value, InvalidPathChars));
    }
    private string _terminal;

    /// <summary>
    /// Gets or sets command-line arguments for the configured terminal application.
    /// </summary>
    public string TerminalArgs
    {
        get => _terminalArgs;
        set => this.RaiseAndSetIfChanged(ref _terminalArgs, value);
    }
    private string _terminalArgs;

    /// <summary>
    /// Gets or sets whether startup should reopen the previous location.
    /// </summary>
    public bool OpenInLastLocation
    {
        get => _openInLastLocation;
        set => this.RaiseAndSetIfChanged(ref _openInLastLocation, value);
    }
    private bool _openInLastLocation;

    /// <summary>
    /// Gets or sets the fallback startup directory.
    /// </summary>
    public string OpenIn
    {
        get => _openIn;
        set => this.RaiseAndSetIfChanged(ref _openIn, SanitizeInput(value, InvalidPathChars));
    }
    private string _openIn;

    /// <summary>
    /// Gets or sets whether dark theme is enabled.
    /// </summary>
    public bool UseDarkMode { get; set; }

    /// <summary>
    /// Gets or sets the selected file display mode.
    /// </summary>
    public string DisplayMode { get; set; }

    /// <summary>
    /// Gets available display mode options.
    /// </summary>
    public IEnumerable<string> DisplayModeOptions { get; } =
        Enum.GetValues<DisplayMode>().Select(option => option.ToString());

    /// <summary>
    /// Gets or sets the selected default sort field.
    /// </summary>
    public string DefaultSort { get; set; }

    /// <summary>
    /// Gets available sort field options.
    /// </summary>
    public IEnumerable<string> SortOptions { get; } =
        Enum.GetValues<SortBy>().Select(option => option.ToString());

    /// <summary>
    /// Gets or sets the file-size threshold used for unit formatting.
    /// </summary>
    public int FileSizeUnitLimit { get; set; }

    /// <summary>
    /// Gets the minimum allowed value for <see cref="FileSizeUnitLimit"/>.
    /// </summary>
    public int FileSizeUnitLimitLowerBound => FileSurferSettings.FileSizeUnitLimitLowerBound;

    /// <summary>
    /// Gets the maximum allowed value for <see cref="FileSizeUnitLimit"/>.
    /// </summary>
    public int FileSizeUnitLimitUpperBound => FileSurferSettings.FileSizeUnitLimitUpperBound;

    /// <summary>
    /// Gets or sets whether sorting order is reversed.
    /// </summary>
    public bool SortReversed { get; set; }

    /// <summary>
    /// Gets or sets whether special folders are shown in the sidebar.
    /// </summary>
    public bool ShowSpecialFolders { get; set; }

    /// <summary>
    /// Gets or sets whether protected files are displayed.
    /// </summary>
    public bool ShowProtectedFiles { get; set; }

    /// <summary>
    /// Gets or sets whether hidden files are displayed.
    /// </summary>
    public bool ShowHiddenFiles { get; set; }

    /// <summary>
    /// Gets or sets whether dot-prefixed files are treated as hidden.
    /// </summary>
    public bool TreatDotFilesAsHidden { get; set; }

    /// <summary>
    /// Gets or sets whether Git integration features are enabled.
    /// </summary>
    public bool GitIntegration { get; set; }

    /// <summary>
    /// Gets or sets whether undo/redo error dialogs are shown.
    /// </summary>
    public bool ShowUndoRedoErrorDialogs { get; set; }

    /// <summary>
    /// Gets or sets whether automatic refresh is enabled.
    /// </summary>
    public bool AutomaticRefresh { get; set; }

    /// <summary>
    /// Gets or sets the automatic refresh interval in milliseconds.
    /// </summary>
    public int AutomaticRefreshInterval { get; set; }

    /// <summary>
    /// Gets the minimum allowed value for <see cref="AutomaticRefreshInterval"/>.
    /// </summary>
    public int AutomaticRefreshIntervalLowerBound =>
        FileSurferSettings.AutomaticRefreshIntervalLowerBound;

    /// <summary>
    /// Gets the maximum allowed value for <see cref="AutomaticRefreshInterval"/>.
    /// </summary>
    public int AutomaticRefreshIntervalUpperBound =>
        FileSurferSettings.AutomaticRefreshIntervalUpperBound;

    /// <summary>
    /// Gets or sets the polling interval for SFTP synchronization in milliseconds.
    /// </summary>
    public int SynchronizerPollingInterval { get; set; }

    /// <summary>
    /// Gets the minimum allowed value for <see cref="SynchronizerPollingInterval"/>.
    /// </summary>
    public int SynchronizerPollingIntervalLowerBound =>
        FileSurferSettings.SynchronizerPollingIntervalLowerBound;

    /// <summary>
    /// Gets the maximum allowed value for <see cref="SynchronizerPollingInterval"/>.
    /// </summary>
    public int SynchronizerPollingIntervalUpperBound =>
        FileSurferSettings.SynchronizerPollingIntervalUpperBound;

    /// <summary>
    /// Initializes the settings view model with the current persisted settings.
    /// </summary>
    public SettingsWindowViewModel() => SetValues(FileSurferSettings.CurrentSettings);

    private void SetValues(SettingsRecord settings)
    {
        NewImageName = settings.newImageName;
        NewTextFileName = settings.newTextFileName;
        NewFileName = settings.newFileName;
        NewDirectoryName = settings.newDirectoryName;
        NotepadApp = settings.notepadApp;
        NotepadAppArgs = settings.notepadAppArgs;
        Terminal = settings.terminal;
        TerminalArgs = settings.terminalArgs;
        OpenInLastLocation = settings.openInLastLocation;
        OpenIn = settings.openIn;
        UseDarkMode = settings.useDarkMode;
        DisplayMode = settings.displayMode;
        DefaultSort = settings.defaultSort;
        FileSizeUnitLimit = settings.fileSizeUnitLimit;
        SortReversed = settings.sortReversed;
        ShowSpecialFolders = settings.showSpecialFolders;
        ShowProtectedFiles = settings.showProtectedFiles;
        ShowHiddenFiles = settings.showHiddenFiles;
        TreatDotFilesAsHidden = settings.treatDotFilesAsHidden;
        GitIntegration = settings.gitIntegration;
        ShowUndoRedoErrorDialogs = settings.showUndoRedoErrorDialogs;
        AutomaticRefresh = settings.automaticRefresh;
        AutomaticRefreshInterval = settings.automaticRefreshInterval;
        SynchronizerPollingInterval = settings.synchronizerPollingInterval;
    }

    /// <summary>
    /// Saves current values using <see cref="FileSurferSettings.ImportSettings(SettingsRecord)"/>
    /// </summary>
    public void Save() =>
        FileSurferSettings.ImportSettings(
            new SettingsRecord
            {
                newImageName = NewImageName.Trim(),
                newTextFileName = NewTextFileName.Trim(),
                newFileName = NewFileName.Trim(),
                newDirectoryName = NewDirectoryName.Trim(),
                notepadApp = NotepadApp.Trim(),
                notepadAppArgs = NotepadAppArgs.Trim(),
                terminal = Terminal.Trim(),
                terminalArgs = TerminalArgs.Trim(),
                openInLastLocation = OpenInLastLocation,
                openIn = OpenIn.Trim(),
                useDarkMode = UseDarkMode,
                displayMode = DisplayMode,
                defaultSort = DefaultSort,
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
                synchronizerPollingInterval = SynchronizerPollingInterval,
                quickAccess = FileSurferSettings.QuickAccess,
            }
        );

    /// <summary>
    /// Resets current values to default
    /// </summary>
    public void ResetToDefault() => SetValues(new SettingsRecord());

    private static string SanitizeInput(string input, char[] invalidChars)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        StringBuilder sb = new(input.Length);
        foreach (char c in input.Where(ch => !invalidChars.Contains(ch)))
            sb.Append(c);

        return sb.ToString();
    }
}
