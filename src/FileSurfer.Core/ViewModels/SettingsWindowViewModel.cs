using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using FileSurfer.Core.Models;
using ReactiveUI;

namespace FileSurfer.Core.ViewModels;

/// <summary>
/// The SettingsWindowViewModel is the ViewModel for the <see cref="Views.SettingsWindow"/>.
/// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
[ // Properties are used by the window, cannot be static have to global
    SuppressMessage("ReSharper", "MemberCanBePrivate.Global"),
    SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global"),
    SuppressMessage("ReSharper", "UnusedMember.Global"),
    SuppressMessage("Performance", "CA1822:Mark members as static"),
]
public sealed class SettingsWindowViewModel : ReactiveObject
{
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public bool IsWindows { get; } = OperatingSystem.IsWindows();

    private string _newImageName;
    public string NewImageName
    {
        get => _newImageName;
        set =>
            this.RaiseAndSetIfChanged(
                ref _newImageName,
                SanitizeInput(value, InvalidFileNameChars)
            );
    }

    private string _newFileName;
    public string NewFileName
    {
        get => _newFileName;
        set =>
            this.RaiseAndSetIfChanged(ref _newFileName, SanitizeInput(value, InvalidFileNameChars));
    }

    private string _newDirectoryName;
    public string NewDirectoryName
    {
        get => _newDirectoryName;
        set =>
            this.RaiseAndSetIfChanged(
                ref _newDirectoryName,
                SanitizeInput(value, InvalidFileNameChars)
            );
    }

    private string _thisPcLabel;
    public string ThisPcLabel
    {
        get => _thisPcLabel;
        set =>
            this.RaiseAndSetIfChanged(ref _thisPcLabel, SanitizeInput(value, InvalidFileNameChars));
    }

    private string _notepadApp;
    public string NotepadApp
    {
        get => _notepadApp;
        set => this.RaiseAndSetIfChanged(ref _notepadApp, SanitizeInput(value, InvalidPathChars));
    }

    public string NotepadAppArgs { get; set; }

    private string _terminal;
    public string Terminal
    {
        get => _terminal;
        set => this.RaiseAndSetIfChanged(ref _terminal, SanitizeInput(value, InvalidPathChars));
    }

    public string TerminalArgs { get; set; }

    private bool _openInLastLocation;
    public bool OpenInLastLocation
    {
        get => _openInLastLocation;
        set => this.RaiseAndSetIfChanged(ref _openInLastLocation, value);
    }

    private string _openIn;
    public string OpenIn
    {
        get => _openIn;
        set => this.RaiseAndSetIfChanged(ref _openIn, SanitizeInput(value, InvalidPathChars));
    }

    public bool UseDarkMode { get; set; }
    public string DisplayMode { get; set; }
    public IEnumerable<string> DisplayModeOptions { get; } =
        Enum.GetValues<DisplayMode>().Select(option => option.ToString());
    public string DefaultSort { get; set; }
    public IEnumerable<string> SortOptions { get; } =
        Enum.GetValues<SortBy>().Select(option => option.ToString());

    public int FileSizeUnitLimit { get; set; }
    public int FileSizeUnitLimitLowerBound => FileSurferSettings.FileSizeUnitLimitLowerBound;
    public int FileSizeUnitLimitUpperBound => FileSurferSettings.FileSizeUnitLimitUpperBound;

    public bool SortReversed { get; set; }
    public bool ShowSpecialFolders { get; set; }
    public bool ShowProtectedFiles { get; set; }
    public bool ShowHiddenFiles { get; set; }
    public bool TreatDotFilesAsHidden { get; set; }
    public bool GitIntegration { get; set; }
    public bool ShowUndoRedoErrorDialogs { get; set; }
    public bool AutomaticRefresh { get; set; }

    public int AutomaticRefreshInterval { get; set; }
    public int AutomaticRefreshIntervalLowerBound =>
        FileSurferSettings.AutomaticRefreshIntervalLowerBound;
    public int AutomaticRefreshIntervalUpperBound =>
        FileSurferSettings.AutomaticRefreshIntervalUpperBound;

    public bool AllowImagePastingFromClipboard { get; set; }

    public SettingsWindowViewModel() => SetValues(FileSurferSettings.CurrentSettings);

    private void SetValues(SettingsRecord settings)
    {
        NewImageName = settings.newImageName;
        NewFileName = settings.newFileName;
        NewDirectoryName = settings.newDirectoryName;
        ThisPcLabel = settings.thisPCLabel;
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
        AllowImagePastingFromClipboard = settings.allowImagePastingFromClipboard;
    }

    /// <summary>
    /// Saves current values using <see cref="FileSurferSettings.ImportSettings(SettingsRecord)"/>
    /// </summary>
    public void Save() =>
        FileSurferSettings.ImportSettings(
            new SettingsRecord
            {
                newImageName = NewImageName.Trim(),
                newFileName = NewFileName.Trim(),
                newDirectoryName = NewDirectoryName.Trim(),
                thisPCLabel = ThisPcLabel.Trim(),
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
                allowImagePastingFromClipboard = AllowImagePastingFromClipboard,
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

        return sb.ToString().Trim().TrimEnd(PathTools.DirSeparator);
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
