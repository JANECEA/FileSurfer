using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.Sftp;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.FileOperations.Undoable;
using ReactiveUI;

namespace FileSurfer.Core.ViewModels;

/// <summary>
/// Bundles sorting state data for easier manipulation in the view.
/// </summary>
public record struct SortInfo(SortBy SortBy, bool SortReversed);

/// <summary>
/// Bundles repository state data for easier manipulation in the view.
/// </summary>
[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
public record struct RepoStateInfo(string CommitsToPull, string CommitsToPush);

/// <summary>
/// Represents a location displayable in the window.
/// </summary>
public record LocationDisplay
{
    private readonly Location _location;
    public string Label { get; init; }
    public string Path { get; init; }

    public LocationDisplay(Location location)
    {
        _location = location;
        Label = location.FileSystem.GetLabel();
        Path = location.Path;
    }

    public Location GetLocation() => _location;

    public virtual bool Equals(LocationDisplay? other) =>
        ReferenceEquals(_location, other?._location);

    public override int GetHashCode() => HashCode.Combine(Label, Path);
}

// ReSharper disable UnusedAutoPropertyAccessor.Global - Used by MainWindow
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CollectionNeverQueried.Global
// ReSharper disable UnusedMember.Global
#pragma warning disable CA1822 // Member can be marked as static.

/// <summary>
/// The MainWindowViewModel is the ViewModel for the main window of the application.
/// <para>
/// It serves as the intermediary between the View and the Model
/// in the MVVM (Model-View-ViewModel) design pattern.
/// </para>
/// Handles data directly bound to the View.
/// </summary>
public sealed partial class MainWindowViewModel : ReactiveObject, IDisposable
{
    private const string EmptyDirMessage = "This directory is empty.";
    private const string EmptySearchMessage = "No items match your query";
    private const string NoRemoteMark = "-";
    private const string ArchiveName = "Archive";

    private readonly UndoRedoHandler<IUndoableFileOperation> _undoRedoHistory;
    private readonly UndoRedoHandler<Location> _locationHistory;
    private readonly SearchManager _searchManager;
    private readonly LocalFileSystem _localFs;
    private readonly IDialogService _dialogService;
    private readonly Action<bool> _setDarkMode;

    /// <summary>
    /// TODO
    /// </summary>
    public required SftpFileSystemFactory SftpFsFactory { private get; init; }

    /// <summary>
    /// TODO
    /// </summary>
    public required IClipboardManager ClipboardManager { private get; init; }

    private DateTime _lastRefreshedUtc;
    private DispatcherTimer? _refreshTimer;

    /// <summary>
    /// TODO
    /// </summary>
    public SortInfo SortInfo =>
        new(FileSurferSettings.SortingMode, FileSurferSettings.SortReversed);

    /// <summary>
    /// Represents locations behind the current one in location history.
    /// </summary>
    public IEnumerable<LocationDisplay> LocationsBack =>
        _locationHistory.EnumerateFromCurrentBack().Select(l => new LocationDisplay(l));

    /// <summary>
    /// Represents locations ahead of the current one in location history.
    /// </summary>
    public IEnumerable<LocationDisplay> LocationsForward =>
        _locationHistory.EnumerateFromCurrentForward().Select(l => new LocationDisplay(l));

    /// <summary>
    /// Holds <see cref="FileSystemEntryViewModel"/>s displayed in the main window.
    /// </summary>
    public ObservableCollection<FileSystemEntryViewModel> FileEntries { get; } = new();

    /// <summary>
    /// Holds the currently selected <see cref="FileSystemEntryViewModel"/>s.
    /// </summary>
    public ObservableCollection<FileSystemEntryViewModel> SelectedFiles { get; } = new();

    /// <summary>
    /// Holds <see cref="FileSystemEntryViewModel"/>s displayed in Quick Access.
    /// </summary>
    public ObservableCollection<SideBarEntryViewModel> QuickAccess { get; } = new();

    /// <summary>
    /// Holds the Special Folders <see cref="FileSystemEntryViewModel"/>s.
    /// </summary>
    public ObservableCollection<SideBarEntryViewModel> SpecialFolders { get; } = [];

    /// <summary>
    /// Holds the Drives <see cref="FileSystemEntryViewModel"/>s.
    /// </summary>
    public ObservableCollection<SideBarEntryViewModel> Drives { get; } = [];

    /// <summary>
    /// Holds the parameters for <see cref="SftpConnection"/>
    /// </summary>
    public ObservableCollection<SftpConnectionViewModel> SftpConnectionsVms { get; } = [];

    /// <summary>
    /// Holds the label currently displayed in the PathBox.
    /// </summary>
    public string CurrentFsLabel
    {
        get => _currentFsLabel;
        set => this.RaiseAndSetIfChanged(ref _currentFsLabel, value);
    }
    private string _currentFsLabel = string.Empty;

    /// <summary>
    /// Holds the text currently displayed in the PathBox.
    /// </summary>
    public string PathBoxText
    {
        get => _pathBoxText;
        set => this.RaiseAndSetIfChanged(ref _pathBoxText, value);
    }
    private string _pathBoxText = string.Empty;

    /// <summary>
    /// Holds the path to the current directory displayed in FileSurfer.
    /// </summary>
    private string CurrentDir
    {
        get => _currentDir;
        set
        {
            _currentDir = value;
            PathBoxText = value;
            SetSearchWaterMark(CurrentDir);
        }
    }
    private string _currentDir = string.Empty;

    private Location CurrentLocation
    {
        get =>
            _currentLocation
            ?? throw new InvalidOperationException("Location was not initialized.");
        set
        {
            _currentLocation = value;
            CurrentDir = value.Path;
            CurrentFs = CurrentLocation.FileSystem;
            CurrentFsLabel = CurrentFs.GetLabel();
            if (FileSurferSettings.OpenInLastLocation && CurrentFs is LocalFileSystem)
                FileSurferSettings.OpenIn = value.Path;

            this.RaisePropertyChanged(nameof(IsLocal));
        }
    }
    private Location? _currentLocation;

    private IFileSystem CurrentFs { get; set; }

    private IPathTools PathTools => CurrentFs.FileInfoProvider.PathTools;

    /// <summary>
    /// TODO
    /// </summary>
    public string? CurrentInfoMessage
    {
        get => _currentInfoMessage;
        set => this.RaiseAndSetIfChanged(ref _currentInfoMessage, value);
    }
    private string? _currentInfoMessage = null;

    /// <summary>
    /// Watermark that will be displayed in the Search bar.
    /// </summary>
    public string SearchWaterMark
    {
        get => _searchWaterMark;
        set => this.RaiseAndSetIfChanged(ref _searchWaterMark, value);
    }
    private string _searchWaterMark = string.Empty;

    /// <summary>
    /// Info about the current directory and selection. Will be displayed in the Status bar.
    /// </summary>
    public string SelectionInfo
    {
        get => _selectionInfo;
        set => this.RaiseAndSetIfChanged(ref _selectionInfo, value);
    }
    private string _selectionInfo = string.Empty;

    /// <summary>
    /// Indicates whether the app is searching currently.
    /// </summary>
    public bool Searching
    {
        get => _searching;
        set
        {
            this.RaiseAndSetIfChanged(ref _searching, value);
            this.RaisePropertyChanged(nameof(NotSearching));
        }
    }
    private bool _searching;

    /// <summary>
    /// Contains a list of current repository's branches, displayed in the "Branches" combobox.
    /// </summary>
    public ObservableCollection<string> Branches { get; } = new();

    /// <summary>
    /// Currently selected branch in the "Branches" combobox.
    /// </summary>
    public string CurrentBranch
    {
        get => _currentBranch;
        set { this.RaiseAndSetIfChanged(ref _currentBranch, value); }
    }
    private string _currentBranch = string.Empty;

    /// <summary>
    /// Indicates whether the current directory is version controlled.
    /// </summary>
    public bool IsVersionControlled
    {
        get => _isVersionControlled;
        set => this.RaiseAndSetIfChanged(ref _isVersionControlled, value);
    }
    private bool _isVersionControlled = false;

    /// <summary>
    /// Holds the current commit repository state info
    /// </summary>
    public RepoStateInfo RepoStateInfo
    {
        get => _repoStateInfo;
        set => this.RaiseAndSetIfChanged(ref _repoStateInfo, value);
    }
    private RepoStateInfo _repoStateInfo = new(string.Empty, string.Empty);

    /// <summary>
    /// Indicates whether there is an opened directory synchronizer window
    /// </summary>
    public bool IsSynchronizerOpen
    {
        get => _isSynchronizerOpen;
        set => this.RaiseAndSetIfChanged(ref _isSynchronizerOpen, value);
    }
    private bool _isSynchronizerOpen = false;

    public ReactiveCommand<Unit, Unit> OpenEntriesCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel, Unit> OpenEntryCommand { get; }
    public ReactiveCommand<SideBarEntryViewModel, Unit> OpenSideBarEntryCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel, Unit> OpenAsCommand { get; }
    public ReactiveCommand<string, Unit> SetNewLocationCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenInNotepadCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel?, Unit> AddToQuickAccessCommand { get; }
    public ReactiveCommand<Unit, Task> AddToArchiveCommand { get; }
    public ReactiveCommand<Unit, Task> ExtractArchiveCommand { get; }
    public ReactiveCommand<LocationDisplay?, Unit> GoBackCommand { get; }
    public ReactiveCommand<LocationDisplay?, Unit> GoForwardCommand { get; }
    public ReactiveCommand<Unit, Unit> GoUpCommand { get; }
    public ReactiveCommand<Unit, Unit> ReloadCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenTerminalCommand { get; }
    public ReactiveCommand<string, Task> SearchCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelSearchCommand { get; }
    public ReactiveCommand<Unit, Unit> NewFileCommand { get; }
    public ReactiveCommand<Unit, Unit> NewDirCommand { get; }
    public ReactiveCommand<string, Unit> RenameCommand { get; }
    public ReactiveCommand<Unit, Unit> CutCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel?, Task> CopyPathCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel, Unit> CreateShortcutCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel, Task> FlattenFolderCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel, Unit> ShowPropertiesCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel?, Task> SyncDirCommand { get; }
    public ReactiveCommand<Unit, Task> PasteCommand { get; }
    public ReactiveCommand<Unit, Task> MoveToTrashCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    public ReactiveCommand<SortBy, Unit> SetSortByCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectNoneCommand { get; }
    public ReactiveCommand<Unit, Unit> InvertSelectionCommand { get; }
    public ReactiveCommand<SftpConnectionViewModel, Unit> OpenSftpCommand { get; }
    public ReactiveCommand<SftpConnectionViewModel, Unit> CloseSftpCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel?, Unit> GitStageCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel?, Unit> GitUnstageCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel?, Unit> GitRestoreCommand { get; }
    public ReactiveCommand<string, Unit> GitSwitchBranchCommand { get; }
    public ReactiveCommand<Unit, Unit> GitStashCommand { get; }
    public ReactiveCommand<Unit, Unit> GitStashPopCommand { get; }
    public ReactiveCommand<Unit, Unit> GitFetchCommand { get; }
    public ReactiveCommand<Unit, Unit> GitPullCommand { get; }
    public ReactiveCommand<string, Unit> GitCommitCommand { get; }
    public ReactiveCommand<Unit, Unit> GitPushCommand { get; }

    /// <summary>
    /// TODO
    /// </summary>
    public bool SelectionNotEmpty => SelectedFiles.Count > 0;

    /// <summary>
    /// TODO
    /// </summary>
    public bool CanGoBack => _locationHistory.GetPrevious() is not null;

    /// <summary>
    /// TODO
    /// </summary>
    public bool CanGoForward => _locationHistory.GetNext() is not null;

    /// <summary>
    /// TODO
    /// </summary>
    public bool IsLocal => CurrentFs is LocalFileSystem;

    /// <summary>
    /// TODO
    /// </summary>
    public bool NotSearching => !Searching;

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="initialDir"></param>
    /// <param name="localFs"></param>
    /// <param name="dialogService"></param>
    /// <param name="setDarkMode"></param>
    public MainWindowViewModel(
        string initialDir,
        LocalFileSystem localFs,
        IDialogService dialogService,
        Action<bool> setDarkMode
    )
    {
        CurrentFs = localFs;
        _localFs = localFs;
        _dialogService = dialogService;
        _setDarkMode = setDarkMode;

        _searchManager = new SearchManager(s => PathBoxText = s, entry => FileEntries.Add(entry));
        _undoRedoHistory = new UndoRedoHandler<IUndoableFileOperation>();
        _locationHistory = new UndoRedoHandler<Location>(() =>
        {
            this.RaisePropertyChanged(nameof(CanGoBack));
            this.RaisePropertyChanged(nameof(CanGoForward));
            this.RaisePropertyChanged(nameof(LocationsBack));
            this.RaisePropertyChanged(nameof(LocationsForward));
        });

        IObservable<bool> isSynchronizing = this.WhenAnyValue(x => x.IsSynchronizerOpen);
        IObservable<bool> canGoForward = this.WhenAnyValue(x => x.CanGoForward);
        IObservable<bool> canGoBack = this.WhenAnyValue(x => x.CanGoBack);
        IObservable<bool> local = this.WhenAnyValue(x => x.IsLocal);
        IObservable<bool> notSearching = this.WhenAnyValue(x => x.NotSearching);
        IObservable<bool> selection = this.WhenAnyValue(x => x.SelectionNotEmpty);
        IObservable<bool> localNotSearching = local.CombineLatest(notSearching, (l, nS) => l && nS);
        IObservable<bool> localSelection = local.CombineLatest(selection, (l, sl) => l && sl);
        IObservable<bool> notLocalNotSearchingNotSync = local.CombineLatest(
            notSearching,
            isSynchronizing,
            (loc, nSearch, nSync) => !loc && nSearch && !nSync
        );

        OpenEntriesCommand = ReactiveCommand.CreateFromTask(
            () => SelectedFiles.Count == 1 ? OpenEntry(SelectedFiles[0]) : OpenEntries(),
            local
        );
        OpenEntryCommand = ReactiveCommand.CreateFromTask<FileSystemEntryViewModel>(OpenEntry);
        OpenSideBarEntryCommand = ReactiveCommand.CreateFromTask<SideBarEntryViewModel>(
            OpenSideBarEntry
        );
        OpenInNotepadCommand = ReactiveCommand.Create(OpenInNotepad, local);
        OpenAsCommand = ReactiveCommand.Create<FileSystemEntryViewModel>(OpenAs, local);
        SetNewLocationCommand = ReactiveCommand.CreateFromTask<string>(SetNewLocation);
        AddToQuickAccessCommand = ReactiveCommand.Create<FileSystemEntryViewModel?>(
            AddToQuickAccess,
            local
        );
        AddToArchiveCommand = ReactiveCommand.Create(AddToArchiveAsync, localNotSearching);
        ExtractArchiveCommand = ReactiveCommand.Create(ExtractArchiveAsync, localNotSearching);
        GoBackCommand = ReactiveCommand.CreateFromTask<LocationDisplay?>(
            l => l is null ? GoBack() : GoBackToLocation(l),
            canGoBack
        );
        GoForwardCommand = ReactiveCommand.CreateFromTask<LocationDisplay?>(
            l => l is null ? GoForward() : GoForwardToLocation(l),
            canGoForward
        );
        GoUpCommand = ReactiveCommand.CreateFromTask(GoUp);
        ReloadCommand = ReactiveCommand.CreateFromTask(HardReload, notSearching);
        OpenTerminalCommand = ReactiveCommand.Create(OpenTerminal, localNotSearching);
        SearchCommand = ReactiveCommand.Create<string, Task>(SearchAsync);
        CancelSearchCommand = ReactiveCommand.Create(CancelSearch);
        NewFileCommand = ReactiveCommand.CreateFromTask(NewFileAsync, notSearching);
        NewDirCommand = ReactiveCommand.CreateFromTask(NewDirAsync, notSearching);
        RenameCommand = ReactiveCommand.CreateFromTask<string>(RenameAsync, selection);
        CutCommand = ReactiveCommand.CreateFromTask(CutAsync, selection);
        CopyCommand = ReactiveCommand.CreateFromTask(CopyAsync, selection);
        CopyPathCommand = ReactiveCommand.Create<FileSystemEntryViewModel?, Task>(CopyPathAsync);
        CreateShortcutCommand = ReactiveCommand.Create<FileSystemEntryViewModel>(CreateShortcut);
        FlattenFolderCommand = ReactiveCommand.Create<FileSystemEntryViewModel, Task>(
            FlattenFolderAsync
        );
        ShowPropertiesCommand = ReactiveCommand.Create<FileSystemEntryViewModel>(ShowProperties);
        SyncDirCommand = ReactiveCommand.Create<FileSystemEntryViewModel?, Task>(
            SynchronizeDirAsync,
            notLocalNotSearchingNotSync
        );
        PasteCommand = ReactiveCommand.Create(PasteAsync, notSearching);
        MoveToTrashCommand = ReactiveCommand.Create(MoveToTrashAsync, localSelection);
        DeleteCommand = ReactiveCommand.Create(Delete, selection);
        SetSortByCommand = ReactiveCommand.Create<SortBy>(SetSortBy);
        UndoCommand = ReactiveCommand.CreateFromTask(UndoAsync);
        RedoCommand = ReactiveCommand.CreateFromTask(RedoAsync);
        SelectAllCommand = ReactiveCommand.Create(SelectAll);
        SelectNoneCommand = ReactiveCommand.Create(SelectNone);
        InvertSelectionCommand = ReactiveCommand.Create(InvertSelection);
        OpenSftpCommand = ReactiveCommand.CreateFromTask<SftpConnectionViewModel>(
            OpenSftpConnectionAsync
        );
        CloseSftpCommand = ReactiveCommand.CreateFromTask<SftpConnectionViewModel>(
            CloseSftpConnection
        );
        GitStageCommand = ReactiveCommand.Create<FileSystemEntryViewModel?>(GitStage);
        GitUnstageCommand = ReactiveCommand.Create<FileSystemEntryViewModel?>(GitUnstage);
        GitRestoreCommand = ReactiveCommand.Create<FileSystemEntryViewModel?>(GitRestore);
        GitSwitchBranchCommand = ReactiveCommand.CreateFromTask<string>(GitSwitchBranchAsync);
        GitStashCommand = ReactiveCommand.Create(GitStash);
        GitStashPopCommand = ReactiveCommand.Create(GitStashPop);
        GitFetchCommand = ReactiveCommand.CreateFromTask(GitFetchAsync);
        GitPullCommand = ReactiveCommand.CreateFromTask(GitPullAsync);
        GitCommitCommand = ReactiveCommand.CreateFromTask<string>(GitCommitAsync);
        GitPushCommand = ReactiveCommand.CreateFromTask(GitPushAsync);

        LoadQuickAccess();
        LoadDrives();
        LoadSftpConnections();
        LoadSettings(false);
        SetInitialLocation(initialDir);
        FileSurferSettings.OnSettingsChange = () => LoadSettings(true);
        SelectedFiles.CollectionChanged += UpdateSelectionInfo;
        FileEntries.CollectionChanged += UpdateSelectionInfo;
    }

    private void LoadQuickAccess()
    {
        foreach (string path in FileSurferSettings.QuickAccess)
        {
            IFileSystemEntry? entry = null;

            ExistsInfo exists = _localFs.LocalFileInfoProvider.Exists(path);
            if (exists.AsDir)
                entry = new DirectoryEntry(path, LocalPathTools.Instance);
            else if (exists.AsFile)
                entry = new FileEntry(path, LocalPathTools.Instance);

            if (entry is not null)
                QuickAccess.Add(new SideBarEntryViewModel(_localFs, entry));
        }
    }

    private void LoadDrives()
    {
        Drives.Clear();
        foreach (DriveEntryInfo driveEntry in _localFs.LocalFileInfoProvider.GetDrives())
            Drives.Add(new SideBarEntryViewModel(_localFs, driveEntry));
    }

    private void LoadSftpConnections()
    {
        foreach (SftpConnection connection in FileSurferSettings.SftpConnections)
            SftpConnectionsVms.Add(new SftpConnectionViewModel(connection));
    }

    private void SetInitialLocation(string localPath)
    {
        Location requestedLocation = new(_localFs, localPath);
        if (!requestedLocation.Exists())
        {
            Location root = new(_localFs, _localFs.LocalFileInfoProvider.GetRoot());
            SetLocation(root);
        }
        SetLocation(requestedLocation);
    }

    private void LoadSettings(bool reload)
    {
        if (!FileSurferSettings.ShowSpecialFolders)
            SpecialFolders.Clear();
        else if (SpecialFolders.Count == 0)
            foreach (SideBarEntryViewModel entry in GetSpecialFolders())
                SpecialFolders.Add(entry);

        if (!FileSurferSettings.AutomaticRefresh)
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;
        }
        else if (_refreshTimer is null)
        {
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(FileSurferSettings.AutomaticRefreshInterval),
            };
            _lastRefreshedUtc = DateTime.UtcNow;
            _refreshTimer.Tick += (_, _) => CheckForUpdates();
            _refreshTimer.Start();
        }

        _setDarkMode(FileSurferSettings.UseDarkMode);
        if (reload)
            HardReload();
    }

    private IEnumerable<SideBarEntryViewModel> GetSpecialFolders() =>
        _localFs
            .LocalFileInfoProvider.GetSpecialFolders()
            .Where(dirPath => !string.IsNullOrEmpty(dirPath))
            .Select(path => new SideBarEntryViewModel(
                _localFs,
                new DirectoryEntry(path, LocalPathTools.Instance)
            ));

    private void UpdateSelectionInfo(object? sender, NotifyCollectionChangedEventArgs? args)
    {
        string selectionInfo = FileEntries.Count == 1 ? "1 item" : $"{FileEntries.Count} items";

        if (SelectedFiles.Count == 1)
            selectionInfo += "  |  1 item selected";
        else if (SelectedFiles.Count > 1)
            selectionInfo += $"  |  {SelectedFiles.Count} items selected";

        bool displaySize = SelectedFiles.Count >= 1;
        long sizeSum = 0;
        foreach (FileSystemEntryViewModel entry in SelectedFiles)
        {
            if (entry.IsDirectory)
            {
                displaySize = false;
                break;
            }
            if (entry.SizeB is long sizeB)
                sizeSum += sizeB;
        }
        if (displaySize)
            selectionInfo += "  " + FileSystemEntryViewModel.GetSizeString(sizeSum);

        SelectionInfo = selectionInfo;
        this.RaisePropertyChanged(nameof(SelectionNotEmpty));
    }

    private void SetSearchWaterMark(string dirPath)
    {
        string dirName = PathTools.GetFileName(dirPath);
        dirName = string.IsNullOrEmpty(dirName) ? CurrentFs.FileInfoProvider.GetRoot() : dirName;
        SearchWaterMark = $"Search {dirName}";
    }

    private void CheckForUpdates() // TODO ASYNC
    {
        if (CurrentLocation.Exists())
        {
            if (CompareSetLastWriteTime())
                HardReload();
            else if (IsVersionControlled)
                SoftReload();
        }
    }

    private bool CompareSetLastWriteTime()
    {
        DateTime lastWriteTimeUtc =
            CurrentFs.FileInfoProvider.GetDirLastWriteUtc(CurrentDir) ?? DateTime.UtcNow;

        if (_lastRefreshedUtc >= lastWriteTimeUtc)
            return false;

        _lastRefreshedUtc = lastWriteTimeUtc;
        return true;
    }

    private void CloseApp()
    {
        FileSurferSettings.QuickAccess = QuickAccess.Select(entry => entry.PathToEntry).ToList();
        FileSurferSettings.SftpConnections = SftpConnectionsVms
            .Select(vm => vm.SftpConnection)
            .ToList();
    }

    /// <summary>
    /// TODO
    /// </summary>
    public void Dispose()
    {
        CloseApp();
        CurrentFs.GitIntegration.Dispose();
        _refreshTimer?.Stop();

        _searchManager.Dispose();

        foreach (SftpConnectionViewModel connection in SftpConnectionsVms)
            connection.FileSystem?.Dispose();

        _localFs.Dispose();
    }
}
