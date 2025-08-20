using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;

namespace FileSurfer.ViewModels;

/// <summary>
/// The SettingsWindowViewModel is the ViewModel for the <see cref="Views.SettingsWindow"/>.
/// </summary>
#pragma warning disable CA1822 // Mark members as static
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
public sealed class SettingsWindowViewModel : ReactiveObject
{
    public string NewImageName { get; set; }
    public string NewFileName { get; set; }
    public string NewDirectoryName { get; set; }
    public string ThisPCLabel { get; set; }
    public string NotepadApp { get; set; }
    public bool OpenInLastLocation
    {
        get => _openInLastLocation;
        set => this.RaiseAndSetIfChanged(ref _openInLastLocation, value);
    }
    private bool _openInLastLocation;
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
    private List<string> QuickAccess { get; set; }

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
        QuickAccess = settings.quickAccess;
    }

    /// <summary>
    /// Saves current values using <see cref="FileSurferSettings.ImportSettings(SettingsRecord)"/>
    /// </summary>
    public void Save() =>
        FileSurferSettings.ImportSettings(
            new SettingsRecord(
                NewImageName,
                NewFileName,
                NewDirectoryName,
                ThisPCLabel,
                NotepadApp,
                OpenInLastLocation,
                OpenIn,
                UseDarkMode,
                DisplayMode,
                DefaultSort,
                FileSizeUnitLimit,
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
            )
        );

    /// <summary>
    /// Resets current values to default based on <see cref="FileSurferSettings.DefaultSettings"/>
    /// </summary>
    public void ResetToDefault() => SetValues(FileSurferSettings.DefaultSettings);
}
#pragma warning restore CA1822 // Mark members as static
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
