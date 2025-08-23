using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ReactiveUI;

namespace FileSurfer.ViewModels;

/// <summary>
/// The SettingsWindowViewModel is the ViewModel for the <see cref="Views.SettingsWindow"/>.
/// </summary>
#pragma warning disable CA1822 // Mark members as static
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
public sealed class SettingsWindowViewModel : ReactiveObject
{
    private string _newImageName;
    public string NewImageName
    {
        get => _newImageName;
        set =>
            this.RaiseAndSetIfChanged(
                ref _newImageName,
                SanitizeInput(
                    value,
                    Path.GetInvalidFileNameChars(),
                    FileSurferSettings.DefaultSettings.newImageName
                )
            );
    }

    private string _newFileName;
    public string NewFileName
    {
        get => _newFileName;
        set =>
            this.RaiseAndSetIfChanged(
                ref _newFileName,
                SanitizeInput(
                    value,
                    Path.GetInvalidFileNameChars(),
                    FileSurferSettings.DefaultSettings.newFileName
                )
            );
    }

    private string _newDirectoryName;
    public string NewDirectoryName
    {
        get => _newDirectoryName;
        set =>
            this.RaiseAndSetIfChanged(
                ref _newDirectoryName,
                SanitizeInput(
                    value,
                    Path.GetInvalidFileNameChars(),
                    FileSurferSettings.DefaultSettings.newDirectoryName
                )
            );
    }

    private string _thisPCLabel;
    public string ThisPCLabel
    {
        get => _thisPCLabel;
        set =>
            this.RaiseAndSetIfChanged(
                ref _thisPCLabel,
                SanitizeInput(
                    value,
                    Path.GetInvalidFileNameChars(),
                    FileSurferSettings.DefaultSettings.thisPCLabel
                )
            );
    }

    private string _notepadApp;
    public string NotepadApp
    {
        get => _notepadApp;
        set =>
            this.RaiseAndSetIfChanged(
                ref _notepadApp,
                SanitizeInput(
                    value,
                    Path.GetInvalidPathChars(),
                    FileSurferSettings.DefaultSettings.notepadApp
                )
            );
    }

    private bool _openInLastLocation;
    public bool OpenInLastLocation
    {
        get => _openInLastLocation;
        set => this.RaiseAndSetIfChanged(ref _openInLastLocation, value);
    }

    public string OpenIn { get; set; }
    public bool UseDarkMode { get; set; }
    public string DisplayMode { get; set; }
    public IEnumerable<string> DisplayModeOptions { get; } =
        Enum.GetValues<DisplayMode>().Select(option => option.ToString());
    public string DefaultSort { get; set; }
    public IEnumerable<string> SortOptions { get; } =
        Enum.GetValues<SortBy>().Select(option => option.ToString());
    public int FileSizeUnitLimit { get; set; }
    public bool SortReversed { get; set; }
    public bool ShowSpecialFolders { get; set; }
    public bool ShowProtectedFiles { get; set; }
    public bool ShowHiddenFiles { get; set; }
    public bool TreatDotFilesAsHidden { get; set; }
    public bool GitIntegration { get; set; }
    public bool ShowUndoRedoErrorDialogs { get; set; }
    public bool AutomaticRefresh { get; set; }
    public int AutomaticRefreshInterval { get; set; }
    public bool AllowImagePastingFromClipboard { get; set; }

    public SettingsWindowViewModel() => SetValues(FileSurferSettings.CurrentSettings);

    private void SetValues(SettingsRecord settings)
    {
        NewImageName = settings.newImageName;
        NewFileName = settings.newFileName;
        NewDirectoryName = settings.newDirectoryName;
        ThisPCLabel = settings.thisPCLabel;
        NotepadApp = settings.notepadApp;
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
                newImageName = NewImageName,
                newFileName = NewFileName,
                newDirectoryName = NewDirectoryName,
                thisPCLabel = ThisPCLabel,
                notepadApp = NotepadApp,
                openInLastLocation = OpenInLastLocation,
                openIn = OpenIn,
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
    /// Resets current values to default based on <see cref="FileSurferSettings.DefaultSettings"/>
    /// </summary>
    public void ResetToDefault() => SetValues(FileSurferSettings.DefaultSettings);

    private static string SanitizeInput(string input, char[] invalidChars, string defaultName)
    {
        if (string.IsNullOrWhiteSpace(input))
            return defaultName;

        StringBuilder sb = new(input.Length);
        foreach (var c in input.Where(ch => !invalidChars.Contains(ch)))
            sb.Append(c);

        return sb.ToString().Trim().TrimEnd('\\', '/');
    }
}
#pragma warning restore CA1822 // Mark members as static
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
