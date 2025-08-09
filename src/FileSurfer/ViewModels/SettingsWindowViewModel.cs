using System;
using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;

namespace FileSurfer.ViewModels;

/// <summary>
/// The SettingsWindowViewModel is the ViewModel for the <see cref="Views.SettingsWindow"/>.
/// </summary>
#pragma warning disable CA1822 // Mark members as static
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
public class SettingsWindowViewModel : ReactiveObject
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
    public DisplayModeEnum DisplayMode { get; set; }
    public SortBy DefaultSort { get; set; }
    public int FileSizeDisplayLimit { get; set; }
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
    public List<string> QuickAccess { get; set; }

    /// <summary>
    /// Invokes <see cref="Save"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    /// <summary>
    /// Invokes <see cref="ResetToDefault"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ResetToDefaultCommand { get; }

    public SettingsWindowViewModel()
    {
        SetValues(FileSurferSettings.GetCurrentSettings());

        SaveCommand = ReactiveCommand.Create(Save);
        ResetToDefaultCommand = ReactiveCommand.Create(ResetToDefault);
    }

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
        DisplayMode = Enum.Parse<DisplayModeEnum>(settings.displayMode);
        DefaultSort = Enum.Parse<SortBy>(settings.defaultSort);
        FileSizeDisplayLimit = settings.fileSizeDisplayLimit;
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
            )
        );

    /// <summary>
    /// Resets current values to default based on <see cref="FileSurferSettings.GetDefaultSettings"/>
    /// </summary>
    public void ResetToDefault() => SetValues(FileSurferSettings.GetDefaultSettings());
}
#pragma warning restore CA1822 // Mark members as static
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
