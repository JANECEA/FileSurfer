using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.Sftp;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.FileOperations.Undoable;
using FileSurfer.Core.Services.Sftp;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.Services.VersionControl;
using FileSurfer.Core.Views;
using FileSurfer.Core.Views.Dialogs;
using ReactiveUI;

namespace FileSurfer.Core.ViewModels;

/// <summary>
/// The MainWindowViewModel is the ViewModel for the main window of the application.
/// <para>
/// It serves as the intermediary between the View and the Model
/// in the MVVM (Model-View-ViewModel) design pattern.
/// </para>
/// Handles data directly bound to the View.
/// </summary>
[ // Properties are used by the window, cannot be static, have to be public
    SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global"),
    SuppressMessage("ReSharper", "MemberCanBePrivate.Global"),
    SuppressMessage("ReSharper", "UnusedMember.Global"),
    SuppressMessage("Performance", "CA1822:Mark members as static"),
]
public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private const string EmptyDirMessage = "This directory is empty.";
    private const string EmptySearchMessage = "No items match your query";

    private readonly IDialogService _dialogService;
    private readonly SearchManager _searchManager;
    private readonly UndoRedoHandler<IUndoableFileOperation> _undoRedoHistory;
    private readonly UndoRedoHandler<Location> _locationHistory;
    private readonly Action<bool> _setDarkMode;
    private readonly LocalFileSystem _localFs;
    private readonly SftpFileSystemFactory _sftpFileSystemFactory;

    private bool _isActionUserInvoked = true;
    private DateTime _lastRefreshedUtc;
    private DispatcherTimer? _refreshTimer;

    public SortInfo SortInfo =>
        new(FileSurferSettings.SortingMode, FileSurferSettings.SortReversed);

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
    public string CurrentDir
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

    public Location CurrentLocation
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
        set
        {
            this.RaiseAndSetIfChanged(ref _currentBranch, value);
            if (_isActionUserInvoked && !string.IsNullOrEmpty(value) && Branches.Contains(value))
            {
                ShowIfError(CurrentFs.GitIntegration.SwitchBranches(value));
                Reload();
            }
        }
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
    /// Indicates whether there is an opened directory synchronizer window
    /// </summary>
    public bool IsSynchronizerOpen
    {
        get => _isSynchronizerOpen;
        set => this.RaiseAndSetIfChanged(ref _isSynchronizerOpen, value);
    }
    private bool _isSynchronizerOpen = false;

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel, Unit> OpenAsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenInNotepadCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel?, Unit> AddToQuickAccessCommand { get; }
    public ReactiveCommand<Unit, Task> AddToArchiveCommand { get; }
    public ReactiveCommand<Unit, Task> ExtractArchiveCommand { get; }
    public ReactiveCommand<Unit, Unit> GoBackCommand { get; }
    public ReactiveCommand<Unit, Unit> GoForwardCommand { get; }
    public ReactiveCommand<Unit, Unit> GoUpCommand { get; }
    public ReactiveCommand<Unit, Unit> ReloadCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenPowerShellCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelSearchCommand { get; }
    public ReactiveCommand<Unit, Unit> NewFileCommand { get; }
    public ReactiveCommand<Unit, Unit> NewDirCommand { get; }
    public ReactiveCommand<Unit, Task> CutCommand { get; }
    public ReactiveCommand<Unit, Task> CopyCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel?, Task> CopyPathCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel, Unit> CreateShortcutCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel, Unit> FlattenFolderCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel, Unit> ShowPropertiesCommand { get; }
    public ReactiveCommand<FileSystemEntryViewModel?, Task> SyncDirCommand { get; }
    public ReactiveCommand<Unit, Task> PasteCommand { get; }
    public ReactiveCommand<Unit, Unit> MoveToTrashCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    public ReactiveCommand<SortBy, Unit> SetSortByCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectNoneCommand { get; }
    public ReactiveCommand<Unit, Unit> InvertSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> PullCommand { get; }
    public ReactiveCommand<Unit, Unit> PushCommand { get; }

    public bool SelectionNotEmpty => SelectedFiles.Count > 0;
    private bool CanGoBack => _locationHistory.GetPrevious() is not null;
    private bool CanGoForward => _locationHistory.GetNext() is not null;
    private bool IsLocal => CurrentFs is LocalFileSystem;
    private bool NotSearching => !Searching;

    /// <summary>
    /// Initializes a new <see cref="MainWindowViewModel"/>.
    /// </summary>
    public MainWindowViewModel(
        string initialDir,
        LocalFileSystem localFs,
        IDialogService dialogService,
        Action<bool> setDarkMode
    )
    {
        _localFs = localFs;
        CurrentFs = localFs;
        _dialogService = dialogService;
        _sftpFileSystemFactory = new SftpFileSystemFactory(dialogService);
        _setDarkMode = setDarkMode;
        _searchManager = new SearchManager(s => PathBoxText = s, entry => FileEntries.Add(entry));
        _undoRedoHistory = new UndoRedoHandler<IUndoableFileOperation>();
        _locationHistory = new UndoRedoHandler<Location>(() =>
        {
            this.RaisePropertyChanged(nameof(CanGoBack));
            this.RaisePropertyChanged(nameof(CanGoForward));
        });
        _locationHistory.AddNewNode(new Location(localFs, localFs.LocalFileInfoProvider.GetRoot()));

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

        OpenCommand = ReactiveCommand.Create(OpenEntries, local);
        OpenInNotepadCommand = ReactiveCommand.Create(OpenInNotepad, local);
        OpenAsCommand = ReactiveCommand.Create<FileSystemEntryViewModel>(OpenAs, local);
        AddToQuickAccessCommand = ReactiveCommand.Create<FileSystemEntryViewModel?>(
            AddToQuickAccess,
            local
        );
        AddToArchiveCommand = ReactiveCommand.Create(AddToArchive, localNotSearching);
        ExtractArchiveCommand = ReactiveCommand.Create(ExtractArchive, localNotSearching);
        GoBackCommand = ReactiveCommand.Create(GoBack, canGoBack);
        GoForwardCommand = ReactiveCommand.Create(GoForward, canGoForward);
        GoUpCommand = ReactiveCommand.Create(GoUp);
        ReloadCommand = ReactiveCommand.Create(() => Reload(true), notSearching);
        OpenPowerShellCommand = ReactiveCommand.Create(OpenPowerShell, localNotSearching);
        CancelSearchCommand = ReactiveCommand.Create(CancelSearch);
        NewFileCommand = ReactiveCommand.Create(NewFile, notSearching);
        NewDirCommand = ReactiveCommand.Create(NewDir, notSearching);
        CutCommand = ReactiveCommand.Create(Cut, selection);
        CopyCommand = ReactiveCommand.Create(Copy, selection);
        CopyPathCommand = ReactiveCommand.Create<FileSystemEntryViewModel?, Task>(CopyPath);
        CreateShortcutCommand = ReactiveCommand.Create<FileSystemEntryViewModel>(CreateShortcut);
        FlattenFolderCommand = ReactiveCommand.Create<FileSystemEntryViewModel>(FlattenFolder);
        ShowPropertiesCommand = ReactiveCommand.Create<FileSystemEntryViewModel>(
            ShowProperties,
            local
        );
        SyncDirCommand = ReactiveCommand.Create<FileSystemEntryViewModel?, Task>(
            SynchronizeDir,
            notLocalNotSearchingNotSync
        );
        PasteCommand = ReactiveCommand.Create(Paste, notSearching);
        MoveToTrashCommand = ReactiveCommand.Create(MoveToTrash, localSelection);
        DeleteCommand = ReactiveCommand.Create(Delete, selection);
        SetSortByCommand = ReactiveCommand.Create<SortBy>(SetSortBy);
        UndoCommand = ReactiveCommand.Create(Undo);
        RedoCommand = ReactiveCommand.Create(Redo);
        SelectAllCommand = ReactiveCommand.Create(SelectAll);
        SelectNoneCommand = ReactiveCommand.Create(SelectNone);
        InvertSelectionCommand = ReactiveCommand.Create(InvertSelection);
        PullCommand = ReactiveCommand.Create(Pull);
        PushCommand = ReactiveCommand.Create(Push);

        LoadQuickAccess();
        LoadDrives();
        LoadSftpConnections();
        LoadSettings(false);
        SetInitialLocation(initialDir);
        FileSurferSettings.OnSettingsChange = () => LoadSettings(true);
        SelectedFiles.CollectionChanged += UpdateSelectionInfo;
        FileEntries.CollectionChanged += UpdateSelectionInfo;
    }

    private void SetInitialLocation(string localPath)
    {
        Location requestedLocation = new(_localFs, localPath);
        if (requestedLocation.Exists())
            SetLocation(requestedLocation);
        else
        {
            Location root = new(_localFs, _localFs.LocalFileInfoProvider.GetRoot());
            SetLocation(root);
            SetLocation(requestedLocation);
        }
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
            _refreshTimer.Tick += CheckForUpdates;
            _refreshTimer.Start();
        }

        _setDarkMode(FileSurferSettings.UseDarkMode);
        if (reload)
            Reload(true);
    }

    /// <summary>
    /// Compares <see cref="_lastRefreshedUtc"/> to the latest <see cref="Directory.GetLastWriteTime(string)"/>.
    /// <para>
    /// Invokes <see cref="Reload"/> if <see cref="Directory.GetLastWriteTime(string)"/> is newer.
    /// </para>
    /// </summary>
    private void CheckForUpdates(object? sender, EventArgs e)
    {
        if (CurrentLocation.Exists())
        {
            if (CompareSetLastWriteTime())
                Reload(true);
            else if (IsVersionControlled)
                Reload(false);
        }
    }

    private bool CompareSetLastWriteTime()
    {
        DateTime lastWriteTimeUtc =
            CurrentFs.FileInfoProvider.GetDirLastModifiedUtc(CurrentDir) ?? DateTime.UtcNow;
        if (_lastRefreshedUtc >= lastWriteTimeUtc)
            return false;

        _lastRefreshedUtc = lastWriteTimeUtc;
        return true;
    }

    /// <summary>
    /// <para>
    /// Checks if <see cref="CurrentDir"/> is version controlled.
    /// </para>
    /// Reloads the <see cref="CurrentDir"/> directory's contents.
    /// <para>
    /// Updates <see cref="SelectionInfo"/>.
    /// </para>
    /// Sets <see cref="SearchWaterMark"/>.
    /// </summary>
    private void Reload(bool forceHardReload = false)
    {
        if (Searching)
            return;

        CheckVersionControl(CurrentLocation);

        IResult result = UpdateEntries(forceHardReload);
        ShowIfError(result);
        if (!result.IsOk)
            return;

        CurrentInfoMessage = FileEntries.Count > 0 ? null : EmptyDirMessage;
        _lastRefreshedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Opens a new <see cref="InfoDialogWindow"/> dialog.
    /// </summary>
    private void ShowError(string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
            _dialogService.InfoDialog("Unexpected error", errorMessage);
    }

    /// <summary>
    /// Opens a new <see cref="InfoDialogWindow"/> dialog if the result is failed.
    /// </summary>
    private void ShowIfError(IResult result)
    {
        if (!result.IsOk)
            foreach (string errorMessage in result.Errors)
                ShowError(errorMessage);
    }

    /// <summary>
    /// Used for setting the text selection when renaming files.
    /// </summary>
    /// <returns>The length of the file name without extension.</returns>
    public int GetNameEndIndex(FileSystemEntryViewModel entry) =>
        SelectedFiles.Count > 0 ? entry.FileSystemEntry.NameWoExtension.Length : 0;

    private void SetSearchWaterMark(string dirPath)
    {
        string? dirName = Path.GetFileName(dirPath);
        dirName = string.IsNullOrEmpty(dirName) ? Path.GetPathRoot(dirPath) : dirName;
        SearchWaterMark = $"Search {dirName}";
    }

    /// <summary>
    /// Update <see cref="SelectionInfo"/> based on the current directory and selection in <see cref="SelectedFiles"/>.
    /// </summary>
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

    /// <summary>
    /// Opens the selected entry.
    /// <para>
    /// If the entry is a directory or a link to a directory, it changes <see cref="CurrentDir"/> to its path.
    /// </para>
    /// <para>
    /// Otherwise, the file is opened in the application preferred by the system.
    /// </para>
    /// </summary>
    public void OpenEntry(IFileSystemEntry entry)
    {
        if (entry is DirectoryEntry or DriveEntry)
            SetNewLocation(entry.PathToEntry);
        else if (CurrentFs.FileInfoProvider.IsLinkedToDirectory(entry.PathToEntry, out string? dir))
            SetNewLocation(dir!);
        else if (CurrentFs is LocalFileSystem fs)
            ShowIfError(fs.LocalShellHandler.OpenFile(entry.PathToEntry));
    }

    public void OpenLocalEntry(SideBarEntryViewModel entry)
    {
        if (entry.FileSystemEntry is DirectoryEntry or DriveEntry)
            SetLocation(_localFs.GetLocation(entry.PathToEntry));
        else if (CurrentFs.FileInfoProvider.IsLinkedToDirectory(entry.PathToEntry, out string? dir))
            SetLocation(_localFs.GetLocation(dir!));
        else
            ShowIfError(_localFs.LocalShellHandler.OpenFile(entry.PathToEntry));
    }

    public async Task OpenSftpConnection(SftpConnectionViewModel connectionVm)
    {
        SftpConnection connection = connectionVm.SftpConnection;

        if (connectionVm.FileSystem is not null)
        {
            string initialDir =
                connection.InitialDirectory ?? connectionVm.FileSystem.FileInfoProvider.GetRoot();
            SetLocation(new Location(connectionVm.FileSystem, initialDir));
            return;
        }
        ValueResult<SftpFileSystem> result = await _sftpFileSystemFactory.TryConnectAsync(
            connection
        );
        ShowIfError(result);
        if (!result.IsOk)
            return;

        SftpFileSystem fileSystem = result.Value;
        connectionVm.FileSystem = fileSystem;

        if (!string.IsNullOrWhiteSpace(connection.InitialDirectory))
        {
            SetLocationNoHistory(new Location(fileSystem, fileSystem.FileInfoProvider.GetRoot()));
            SetLocation(new Location(fileSystem, connection.InitialDirectory));
        }
        else
            SetLocation(new Location(fileSystem, fileSystem.FileInfoProvider.GetRoot()));
    }

    public void CloseSftpConnection(SftpConnectionViewModel connectionVm)
    {
        if (connectionVm.FileSystem is null)
        {
            ShowError("Connection is not active.");
            return;
        }
        SftpFileSystem fileSystem = connectionVm.FileSystem;
        fileSystem.Dispose();
        connectionVm.FileSystem = null;
        if (ReferenceEquals(fileSystem, CurrentFs))
            SetLocation(new Location(_localFs, _localFs.LocalFileInfoProvider.GetRoot()));
    }

    /// <summary>
    /// Shows the "Open file with" dialog.
    /// </summary>
    public void OpenAs(FileSystemEntryViewModel entry)
    {
        if (!entry.IsDirectory)
            ShowIfError(CurrentFs.FileProperties.ShowOpenAsDialog(entry.FileSystemEntry));
    }

    /// <summary>
    /// Opens multiple files at once.
    /// </summary>
    public void OpenEntries()
    {
        if (SelectedFiles.Count > 1)
        {
            foreach (FileSystemEntryViewModel entry in SelectedFiles)
                if (
                    entry.IsDirectory
                    || CurrentFs.FileInfoProvider.IsLinkedToDirectory(entry.PathToEntry, out _)
                )
                    return;
        }
        // To prevent collection changing during iteration
        foreach (FileSystemEntryViewModel entry in SelectedFiles.ConvertToArray())
            OpenEntry(entry.FileSystemEntry);
    }

    /// <summary>
    /// Opens the selected files in the notepad app specified in <see cref="FileSurferSettings.NotepadApp"/>
    /// </summary>
    public void OpenInNotepad()
    {
        if (CurrentFs is not LocalFileSystem fs)
            return;

        foreach (FileSystemEntryViewModel entry in SelectedFiles)
            if (!entry.IsDirectory)
                ShowIfError(fs.LocalShellHandler.OpenInNotepad(entry.PathToEntry));
    }

    /// <summary>
    /// Navigates up one directory level from the current directory.
    /// <para>
    /// - If the current directory is already at the root level or if the parent directory
    ///   cannot be determined, it sets the current directory to a special "This PC" label.
    /// </para>
    /// - Otherwise, it updates the current directory to its parent directory.
    /// </summary>
    public void GoUp()
    {
        if (Path.GetDirectoryName(PathTools.NormalizePath(CurrentDir)) is string parentDir)
            SetNewLocation(parentDir);
    }

    private IEnumerable<SideBarEntryViewModel> GetSpecialFolders() =>
        _localFs
            .LocalFileInfoProvider.GetSpecialFolders()
            .Where(dirPath => !string.IsNullOrEmpty(dirPath))
            .Select(path => new SideBarEntryViewModel(_localFs, new DirectoryEntry(path)));

    private void LoadDrives()
    {
        Drives.Clear();
        foreach (DriveEntry driveEntry in _localFs.LocalFileInfoProvider.GetDrives())
            Drives.Add(new SideBarEntryViewModel(_localFs, driveEntry));
    }

    private void LoadQuickAccess()
    {
        foreach (string path in FileSurferSettings.QuickAccess)
            if (_localFs.LocalFileInfoProvider.DirectoryExists(path))
                QuickAccess.Add(new SideBarEntryViewModel(_localFs, new DirectoryEntry(path)));
            else if (_localFs.LocalFileInfoProvider.FileExists(path))
                QuickAccess.Add(new SideBarEntryViewModel(_localFs, new FileEntry(path)));
    }

    public void AddToQuickAccess(FileSystemEntryViewModel? entry) =>
        QuickAccess.Add(
            entry is not null
                ? new SideBarEntryViewModel(_localFs, entry.FileSystemEntry)
                : new SideBarEntryViewModel(_localFs, new DirectoryEntry(CurrentDir))
        );

    private void LoadSftpConnections()
    {
        foreach (SftpConnection connection in FileSurferSettings.SftpConnections)
            SftpConnectionsVms.Add(new SftpConnectionViewModel(connection));
    }

    private IResult UpdateEntries(bool forceHardReload)
    {
        if (forceHardReload || CompareSetLastWriteTime())
            return LoadDirEntries(CurrentLocation);

        if (IsVersionControlled)
            foreach (FileSystemEntryViewModel entry in FileEntries)
                entry.UpdateGitStatus(GetGitStatus(entry.PathToEntry, CurrentFs));

        return SimpleResult.Ok();
    }

    private IResult LoadDirEntries(Location location)
    {
        IFileSystem fs = location.FileSystem;
        var dirsResult = fs.FileInfoProvider.GetPathDirs(
            location.Path,
            FileSurferSettings.ShowHiddenFiles,
            FileSurferSettings.ShowProtectedFiles
        );
        var filesResult = fs.FileInfoProvider.GetPathFiles(
            location.Path,
            FileSurferSettings.ShowHiddenFiles,
            FileSurferSettings.ShowProtectedFiles
        );
        if (!dirsResult.IsOk || !filesResult.IsOk)
            return Result.Error(dirsResult.Errors);

        FileSystemEntryViewModel[] dirs = dirsResult.Value.ConvertToArray(
            entry => new FileSystemEntryViewModel(fs, entry, GetGitStatus(entry.PathToEntry, fs))
        );
        FileSystemEntryViewModel[] files = filesResult.Value.ConvertToArray(
            entry => new FileSystemEntryViewModel(fs, entry, GetGitStatus(entry.PathToEntry, fs))
        );

        AddEntries(dirs, files);
        return SimpleResult.Ok();
    }

    private GitStatus GetGitStatus(string path, IFileSystem fileSystem) =>
        IsVersionControlled
            ? fileSystem.GitIntegration.GetStatus(path)
            : GitStatus.NotVersionControlled;

    /// <summary>
    /// Adds directories and files to <see cref="FileEntries"/>.
    /// </summary>
    private void AddEntries(FileSystemEntryViewModel[] dirs, FileSystemEntryViewModel[] files)
    {
        SortBy sortBy = FileSurferSettings.SortingMode;
        bool sortReversed = FileSurferSettings.SortReversed;

        SortInPlace(files, sortBy);
        SortInPlace(dirs, sortBy);

        HashSet<string>? selectedPaths = null;
        if (SelectedFiles.Count > 0)
            selectedPaths = SelectedFiles.Select(entry => entry.Name).ToHashSet();

        FileEntries.Clear();
        if (!sortReversed || sortBy is SortBy.Type or SortBy.Size)
            for (int i = 0; i < dirs.Length; i++)
                FileEntries.Add(dirs[i]);
        else
            for (int i = dirs.Length - 1; i >= 0; i--)
                FileEntries.Add(dirs[i]);

        if (!sortReversed)
            for (int i = 0; i < files.Length; i++)
                FileEntries.Add(files[i]);
        else
            for (int i = files.Length - 1; i >= 0; i--)
                FileEntries.Add(files[i]);

        if (selectedPaths is null)
            return;

        foreach (FileSystemEntryViewModel entry in FileEntries)
            if (selectedPaths.Contains(entry.Name))
                SelectedFiles.Add(entry);
    }

    private static void SortInPlace(FileSystemEntryViewModel[] entries, SortBy sortBy)
    {
        Comparison<FileSystemEntryViewModel>[] comparisons =
        [
            (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
            (a, b) => DateTime.Compare(a.LastModTime, b.LastModTime),
            (a, b) => string.Compare(a.Type, b.Type, StringComparison.OrdinalIgnoreCase),
            (a, b) => (a.SizeB ?? 0).CompareTo(b.SizeB ?? 0),
        ];
        Array.Sort(entries, Comparison);
        return;

        int Comparison(FileSystemEntryViewModel a, FileSystemEntryViewModel b)
        {
            Comparison<FileSystemEntryViewModel> preferred = comparisons[(int)sortBy];
            int compVal;
            if ((compVal = preferred(a, b)) != 0)
                return compVal;

            foreach (Comparison<FileSystemEntryViewModel> comp in comparisons)
                if ((compVal = comp(a, b)) != 0)
                    return compVal;

            return 0;
        }
    }

    /// <summary>
    /// Sets <see cref="IsVersionControlled"/> and updates <see cref="Branches"/>.
    /// </summary>
    private void CheckVersionControl(Location location)
    {
        IsVersionControlled =
            FileSurferSettings.GitIntegration
            && location.FileSystem.FileInfoProvider.DirectoryExists(location.Path)
            && location.FileSystem.GitIntegration.InitIfGitRepository(location.Path);

        LoadBranches(location);
    }

    private void LoadBranches(Location location)
    {
        if (!IsVersionControlled)
        {
            Branches.Clear();
            return;
        }

        string currentBranch = location.FileSystem.GitIntegration.GetCurrentBranchName();
        string[] branches = location.FileSystem.GitIntegration.GetBranches();
        if (CurrentBranch == currentBranch && Branches.EqualsUnordered(branches))
            return;

        Branches.Clear();
        foreach (string branch in branches)
        {
            Branches.Add(branch);

            if (string.Equals(currentBranch, branch, StringComparison.Ordinal))
                currentBranch = branch; // Object references must match
        }

        _isActionUserInvoked = false;
        CurrentBranch = currentBranch;
        _isActionUserInvoked = true;
    }

    private IResult SetLocationNoHistory(Location location)
    {
        if (!location.Exists())
            return SimpleResult.Error(
                $"Location {location.FileSystem.GetLabel()}:{location.Path} does not exist."
            );

        if (Searching)
            CancelSearch();

        CheckVersionControl(location);
        IResult result = LoadDirEntries(location);
        if (!result.IsOk)
            return result;

        CurrentLocation = location;
        _lastRefreshedUtc = DateTime.UtcNow;
        Reload(false);
        return SimpleResult.Ok();
    }

    public void SetLocation(Location location)
    {
        IResult result = SetLocationNoHistory(location);

        ShowIfError(result);
        if (result.IsOk && !location.Equals(_locationHistory.Current))
            _locationHistory.AddNewNode(location);
    }

    public void SetNewLocation(string path)
    {
        Location location = CurrentFs.GetLocation(path);
        SetLocation(location);
    }

    /// <summary>
    /// Sets the current directory to the previous directory in <see cref="_locationHistory"/>.
    /// <para>
    /// Removes the directory from <see cref="_locationHistory"/> in case it's invalid.
    /// </para>
    /// </summary>
    public void GoBack()
    {
        if (Searching)
            CancelSearch();

        while (_locationHistory.GetPrevious() is Location previousLocation)
        {
            _locationHistory.MoveToPrevious();

            if (previousLocation.Exists())
            {
                SetLocationNoHistory(previousLocation);
                return;
            }
            _locationHistory.RemoveNode(false);
        }
    }

    /// <summary>
    /// Sets the current directory to the next directory in <see cref="_locationHistory"/>.
    /// <para>
    /// Removes the directory from <see cref="_locationHistory"/> in case it's invalid.
    /// </para>
    /// </summary>
    public void GoForward()
    {
        if (Searching)
            CancelSearch();

        while (_locationHistory.GetNext() is Location nextLocation)
        {
            _locationHistory.MoveToNext();

            if (nextLocation.Exists())
            {
                SetLocationNoHistory(nextLocation);
                return;
            }
            _locationHistory.RemoveNode(true);
        }
    }

    /// <summary>
    /// Opens PowerShell in <see cref="CurrentDir"/> if possible.
    /// </summary>
    private void OpenPowerShell()
    {
        if (CurrentFs is LocalFileSystem fs && fs.LocalFileInfoProvider.DirectoryExists(CurrentDir))
            ShowIfError(fs.LocalShellHandler.OpenCmdAt(CurrentDir));
    }

    public async Task SearchAsync(string searchQuery)
    {
        if (Searching)
        {
            CurrentInfoMessage = null;
            await _searchManager.CancelSearchAsync();
        }
        Searching = true;
        FileEntries.Clear();

        int? foundEntries = await _searchManager.SearchAsync(CurrentFs, searchQuery, CurrentDir);
        if (foundEntries is 0)
            CurrentInfoMessage = EmptySearchMessage;
    }

    public void CancelSearch()
    {
        Searching = false;
        _searchManager.CancelSearch();

        if (_locationHistory.Current is Location location)
        {
            IResult result = SetLocationNoHistory(location);
            if (!result.IsOk)
                GoBack();
        }
    }

    /// <summary>
    /// Creates a new file using <see cref="CurrentFs"/> in <see cref="CurrentDir"/>
    /// with the name specified in <see cref="FileSurferSettings.NewFileName"/>.
    /// <para>
    /// Invokes <see cref="Reload"/> and adds a new <see cref="NewFileAt"/> operation
    /// to <see cref="_undoRedoHistory"/> if it was a success.
    /// </para>
    /// </summary>
    private void NewFile()
    {
        string newFileName = FileNameGenerator.GetAvailableName(
            CurrentFs.FileInfoProvider,
            CurrentDir,
            FileSurferSettings.NewFileName
        );

        NewFileAt operation = new(CurrentFs.FileIoHandler, CurrentDir, newFileName);
        IResult result = operation.Invoke();
        if (result.IsOk)
        {
            Reload();
            _undoRedoHistory.AddNewNode(operation);
            SelectedFiles.Add(FileEntries.First(entry => entry.Name == newFileName));
        }
        ShowIfError(result);
    }

    /// <summary>
    /// Creates a new directory using <see cref="CurrentFs"/> in <see cref="CurrentDir"/>
    /// with the name specified in <see cref="FileSurferSettings.NewDirectoryName"/>.
    /// <para>
    /// Invokes <see cref="Reload"/> and adds a new <see cref="NewDirAt"/> operation
    /// to <see cref="_undoRedoHistory"/> if it was a success.
    /// </para>
    /// </summary>
    private void NewDir()
    {
        string newDirName = FileNameGenerator.GetAvailableName(
            CurrentFs.FileInfoProvider,
            CurrentDir,
            FileSurferSettings.NewDirectoryName
        );

        NewDirAt operation = new(CurrentFs.FileIoHandler, CurrentDir, newDirName);
        IResult result = operation.Invoke();
        if (result.IsOk)
        {
            Reload();
            _undoRedoHistory.AddNewNode(operation);
            SelectedFiles.Add(FileEntries.First(entry => entry.Name == newDirName));
        }
        ShowIfError(result);
    }

    /// <summary>
    /// Creates an archive from the <see cref="FileSystemEntryViewModel"/>s in <see cref="SelectedFiles"/>.
    /// </summary>
    public async Task AddToArchive()
    {
        if (!CurrentFs.FileInfoProvider.DirectoryExists(CurrentDir))
            return;

        string archiveName =
            SelectedFiles[^1].FileSystemEntry.NameWoExtension
            + LocalArchiveManager.ArchiveTypeExtension;

        ShowIfError(
            await Task.Run(() =>
                CurrentFs.ArchiveManager.ZipFiles(
                    SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry),
                    CurrentDir,
                    FileNameGenerator.GetAvailableName(
                        CurrentFs.FileInfoProvider,
                        CurrentDir,
                        archiveName
                    )
                )
            )
        );
        Reload();
    }

    /// <summary>
    /// Extracts the archives selected in <see cref="SelectedFiles"/>.
    /// </summary>
    public async Task ExtractArchive()
    {
        if (!CurrentFs.FileInfoProvider.DirectoryExists(CurrentDir))
            return;

        foreach (FileSystemEntryViewModel entry in SelectedFiles)
            if (!CurrentFs.ArchiveManager.IsZipped(entry.PathToEntry))
            {
                ShowError($"Entry \"{entry.Name}\" is not an archive.");
                return;
            }

        string[] archivePaths = SelectedFiles.ConvertToArray(e => e.PathToEntry);
        await Task.Run(() =>
        {
            foreach (string path in archivePaths)
                ShowIfError(CurrentFs.ArchiveManager.UnzipArchive(path, CurrentDir));
        });
        Reload();
    }

    /// <summary>
    /// Copies the path to the selected <see cref="FileSystemEntryViewModel"/> to the system clipboard.
    /// </summary>
    public async Task CopyPath(FileSystemEntryViewModel? entry) =>
        ShowIfError(
            await _localFs.LocalClipboardManager.CopyPathToFileAsync(
                entry is null ? CurrentDir : entry.PathToEntry
            )
        );

    /// <summary>
    /// Relays the current selection in <see cref="SelectedFiles"/> to <see cref="IClipboardManager"/>.
    /// </summary>
    public async Task Cut()
    {
        if (SelectedFiles.Count > 0)
            ShowIfError(
                await CurrentFs.ClipboardManager.CutAsync(
                    SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry),
                    CurrentDir
                )
            );
    }

    /// <summary>
    /// Relays the current selection in <see cref="SelectedFiles"/> to <see cref="IClipboardManager"/>.
    /// </summary>
    public async Task Copy()
    {
        if (SelectedFiles.Count > 0)
            ShowIfError(
                await CurrentFs.ClipboardManager.CopyAsync(
                    SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry),
                    CurrentDir
                )
            );
    }

    /// <summary>
    /// Determines the type of paste operation and executes it using <see cref="IClipboardManager"/>.
    /// <para>
    /// Adds the appropriate <see cref="IUndoableFileOperation"/> to <see cref="_locationHistory"/> if the operation was a success.
    /// </para>
    /// Invokes <see cref="Reload"/>.
    /// </summary>
    private async Task Paste()
    {
        if (
            FileSurferSettings.AllowImagePastingFromClipboard
            && CurrentFs is LocalFileSystem fileSystem
            && (await fileSystem.LocalClipboardManager.PasteImageAsync(CurrentDir)).IsOk
        )
            return;

        PasteType pasteType = await CurrentFs.ClipboardManager.GetOperationType(CurrentDir);

        ValueResult<IUndoableFileOperation> result =
            pasteType == PasteType.Duplicate ? await DuplicateFiles() : await PasteFiles(pasteType);

        if (result.IsOk)
            _undoRedoHistory.AddNewNode(result.Value);

        ShowIfError(result);
        Reload();
    }

    private async Task<ValueResult<IUndoableFileOperation>> PasteFiles(PasteType pasteType)
    {
        ValueResult<IFileSystemEntry[]> pastedResult = await CurrentFs.ClipboardManager.PasteAsync(
            CurrentDir,
            pasteType
        );
        if (!pastedResult.IsOk)
            return ValueResult<IUndoableFileOperation>.Error(pastedResult);

        return ValueResult<IUndoableFileOperation>.Ok(
            pasteType is PasteType.Cut
                ? new MoveFilesTo(CurrentFs.FileIoHandler, pastedResult.Value, CurrentDir)
                : new CopyFilesTo(CurrentFs.FileIoHandler, pastedResult.Value, CurrentDir)
        );
    }

    private async Task<ValueResult<IUndoableFileOperation>> DuplicateFiles()
    {
        IFileSystemEntry[] clipboard = CurrentFs.ClipboardManager.GetClipboard();

        ValueResult<string[]> copyNamesResult = await CurrentFs.ClipboardManager.DuplicateAsync(
            CurrentDir
        );
        if (!copyNamesResult.IsOk)
            return ValueResult<IUndoableFileOperation>.Error(copyNamesResult);

        return ValueResult<IUndoableFileOperation>.Ok(
            new DuplicateFiles(CurrentFs.FileIoHandler, clipboard, copyNamesResult.Value)
        );
    }

    /// <summary>
    /// Relays the operation to <see cref="IShellHandler"/> and invokes <see cref="Reload"/>.
    /// </summary>
    public void CreateShortcut(FileSystemEntryViewModel entry)
    {
        IResult result = entry.IsDirectory
            ? CurrentFs.ShellHandler.CreateDirectoryLink(entry.PathToEntry)
            : CurrentFs.ShellHandler.CreateFileLink(entry.PathToEntry);

        ShowIfError(result);
        Reload();
    }

    /// <summary>
    /// Relays the operation to <see cref="IFileProperties"/>.
    /// </summary>
    public void ShowProperties(FileSystemEntryViewModel entry) =>
        ShowIfError(CurrentFs.FileProperties.ShowFileProperties(entry));

    private async Task SynchronizeDir(FileSystemEntryViewModel? entry)
    {
        if (IsSynchronizerOpen)
        {
            ShowError("Another directory is being synchronized.");
            return;
        }
        if (CurrentFs is not SftpFileSystem sftpFs)
        {
            ShowError("Cannot synchronize with a local directory.");
            return;
        }

        Location remoteLocation = entry is null
            ? CurrentLocation
            : sftpFs.GetLocation(entry.PathToEntry);

        ValueResult<string> localPathResult = await SftpSynchronizerHelper.GetLocalPath(
            remoteLocation,
            _locationHistory.GetCurrentCollectionReversed(),
            _dialogService
        );
        ShowIfError(localPathResult);
        if (!localPathResult.IsOk)
            return;

        SftpSynchronizerWindow syncWindow = new();
        syncWindow.DataContext = new SftpSynchronizerViewModel(
            new AvaloniaDialogService(syncWindow),
            new Location(_localFs, localPathResult.Value),
            remoteLocation
        );

        IsSynchronizerOpen = true;
        syncWindow.Closed += (_, _) => IsSynchronizerOpen = false;
        syncWindow.Show();
    }

    /// <summary>
    /// Relays the operation to <see cref="RenameOne(string)"/> or <see cref="RenameMultiple(string)"/>.
    /// </summary>
    public void Rename(string newName)
    {
        newName = newName.Trim();

        if (SelectedFiles.Count == 1)
            RenameOne(newName);
        else if (SelectedFiles.Count > 1)
            RenameMultiple(newName);
    }

    private void RenameOne(string newName)
    {
        FileSystemEntryViewModel entry = SelectedFiles[0];

        RenameOne operation = new(CurrentFs.FileIoHandler, entry.FileSystemEntry, newName);
        IResult result = operation.Invoke();
        if (result.IsOk)
        {
            _undoRedoHistory.AddNewNode(operation);
            Reload();

            FileSystemEntryViewModel? newEntry = FileEntries.FirstOrDefault(e =>
                PathTools.NamesAreEqual(e.Name, newName)
            );
            if (newEntry is not null)
                SelectedFiles.Add(newEntry);
        }
        ShowIfError(result);
    }

    private void RenameMultiple(string namingPattern)
    {
        IFileSystemEntry[] entries = SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry);
        if (!FileNameGenerator.CanBeRenamedCollectively(entries))
        {
            ShowError("Selected entries aren't of the same type.");
            return;
        }

        RenameMultiple operation = new(
            CurrentFs.FileIoHandler,
            entries,
            FileNameGenerator.GetAvailableNames(CurrentFs.FileInfoProvider, entries, namingPattern)
        );
        IResult result = operation.Invoke();
        if (result.IsOk)
            _undoRedoHistory.AddNewNode(operation);

        ShowIfError(result);
        Reload();
    }

    /// <summary>
    /// Moves the <see cref="FileSystemEntryViewModel"/>s in <see cref="SelectedFiles"/> to the system trash using <see cref="CurrentFs"/>.
    /// <para>
    /// Adds the operation to <see cref="_undoRedoHistory"/> if the operation was successful.
    /// </para>
    /// Invokes <see cref="Reload"/>.
    /// </summary>
    public void MoveToTrash()
    {
        MoveFilesToTrash operation = new(
            CurrentFs.BinInteraction,
            CurrentFs.FileIoHandler,
            SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry)
        );

        IResult result = operation.Invoke();
        if (result.IsOk)
            _undoRedoHistory.AddNewNode(operation);

        ShowIfError(result);
        Reload();
    }

    public void FlattenFolder(FileSystemEntryViewModel entry)
    {
        if (!entry.IsDirectory)
        {
            ShowError($"Cannot flatten a file: \"{entry.Name}\".");
            return;
        }

        FlattenFolder action = new(
            CurrentFs.FileIoHandler,
            CurrentFs.FileInfoProvider,
            entry.PathToEntry
        );
        IResult result = action.Invoke();
        if (result.IsOk)
            _undoRedoHistory.AddNewNode(action);

        ShowIfError(result);
        Reload();
    }

    /// <summary>
    /// Permanently deletes the <see cref="FileSystemEntryViewModel"/>s in <see cref="SelectedFiles"/>.
    /// <para>
    /// Invokes <see cref="Reload"/>.
    /// </para>
    /// </summary>
    public void Delete()
    {
        foreach (FileSystemEntryViewModel entry in SelectedFiles)
            ShowIfError(
                entry.IsDirectory
                    ? CurrentFs.FileIoHandler.DeleteDir(entry.PathToEntry)
                    : CurrentFs.FileIoHandler.DeleteFile(entry.PathToEntry)
            );

        Reload();
    }

    /// <summary>
    /// Sets <see cref="SortBy"/> to the parameter and determines <see cref="FileSurferSettings.SortReversed"/>.
    /// <para>
    /// Invokes <see cref="Reload"/>.
    /// </para>
    /// </summary>
    private void SetSortBy(SortBy sortBy)
    {
        FileSurferSettings.SortReversed =
            FileSurferSettings.SortingMode == sortBy && !FileSurferSettings.SortReversed;

        FileSurferSettings.SortingMode = sortBy;
        Reload(true);
    }

    /// <summary>
    /// Invokes <see cref="IUndoableFileOperation.Undo()"/> on the current
    /// <see cref="IUndoableFileOperation"/> and goes back in <see cref="_undoRedoHistory"/>.
    /// </summary>
    private void Undo()
    {
        if (_undoRedoHistory.IsTail())
            _undoRedoHistory.MoveToPrevious();

        if (_undoRedoHistory.IsHead())
            return;

        IUndoableFileOperation operation =
            _undoRedoHistory.Current ?? throw new InvalidOperationException();

        IResult result = operation.Undo();
        if (result.IsOk)
        {
            _undoRedoHistory.MoveToPrevious();
            Reload();
        }
        else
        {
            if (FileSurferSettings.ShowUndoRedoErrorDialogs)
                ShowIfError(result);

            _undoRedoHistory.RemoveNode(true);
        }
    }

    /// <summary>
    /// Moves forward in <see cref="_undoRedoHistory"/> and invokes
    /// <see cref="IUndoableFileOperation.Invoke()"/> on the current <see cref="IUndoableFileOperation"/>.
    /// </summary>
    private void Redo()
    {
        _undoRedoHistory.MoveToNext();
        if (_undoRedoHistory.Current is null)
            return;

        IUndoableFileOperation operation = _undoRedoHistory.Current;
        IResult result = operation.Invoke();
        if (result.IsOk)
            Reload();
        else
        {
            if (FileSurferSettings.ShowUndoRedoErrorDialogs)
                ShowIfError(result);

            _undoRedoHistory.RemoveNode(true);
        }
    }

    /// <summary>
    /// Adds all <see cref="FileSystemEntryViewModel"/>s in <see cref="FileEntries"/> to <see cref="SelectedFiles"/>.
    /// </summary>
    private void SelectAll()
    {
        SelectedFiles.Clear();
        foreach (FileSystemEntryViewModel entry in FileEntries)
            SelectedFiles.Add(entry);
    }

    /// <summary>
    /// Clears <see cref="SelectedFiles"/>.
    /// </summary>
    private void SelectNone() => SelectedFiles.Clear();

    /// <summary>
    /// Inverts the current selection in <see cref="SelectedFiles"/> compared to <see cref="FileEntries"/>.
    /// </summary>
    private void InvertSelection()
    {
        HashSet<string> oldSelectionNames = SelectedFiles.Select(entry => entry.Name).ToHashSet();

        SelectedFiles.Clear();
        foreach (FileSystemEntryViewModel entry in FileEntries)
        {
            if (!oldSelectionNames.Contains(entry.Name))
                SelectedFiles.Add(entry);
        }
    }

    /// <summary>
    /// Relays the operations to <see cref="IFileIoHandler"/>.
    /// </summary>
    public void StageFile(FileSystemEntryViewModel entry)
    {
        if (IsVersionControlled)
            ShowIfError(CurrentFs.GitIntegration.StagePath(entry.PathToEntry));
    }

    /// <summary>
    /// Relays the operations to <see cref="IGitIntegration"/>.
    /// </summary>
    public void UnstageFile(FileSystemEntryViewModel entry)
    {
        if (IsVersionControlled)
            ShowIfError(CurrentFs.GitIntegration.UnstagePath(entry.PathToEntry));
    }

    /// <summary>
    /// Relays the operations to <see cref="IGitIntegration"/>.
    /// </summary>
    private void Pull()
    {
        if (IsVersionControlled)
        {
            ShowIfError(CurrentFs.GitIntegration.PullChanges());
            Reload();
        }
    }

    /// <summary>
    /// Relays the operations to <see cref="IGitIntegration"/>.
    /// </summary>
    public void Commit(string commitMessage)
    {
        if (IsVersionControlled)
        {
            ShowIfError(CurrentFs.GitIntegration.CommitChanges(commitMessage));
            Reload();
        }
    }

    /// <summary>
    /// Relays the operations to <see cref="IGitIntegration"/>.
    /// </summary>
    private void Push()
    {
        if (IsVersionControlled)
            ShowIfError(CurrentFs.GitIntegration.PushChanges());
    }

    /// <summary>
    /// Call on app closing
    /// </summary>
    public void CloseApp()
    {
        FileSurferSettings.QuickAccess = QuickAccess.Select(entry => entry.PathToEntry).ToList();
        FileSurferSettings.SftpConnections = SftpConnectionsVms
            .Select(vm => vm.SftpConnection)
            .ToList();
    }

    public void Dispose()
    {
        CurrentFs.GitIntegration.Dispose();
        _refreshTimer?.Stop();

        _searchManager.Dispose();

        foreach (SftpConnectionViewModel connection in SftpConnectionsVms)
            connection.FileSystem?.Dispose();
    }
}
