using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FileSurfer.Models;
using FileSurfer.Models.FileInformation;
using FileSurfer.Models.FileOperations;
using FileSurfer.Models.FileOperations.Undoable;
using FileSurfer.Models.Shell;
using FileSurfer.Models.VersionControl;
using ReactiveUI;

namespace FileSurfer.ViewModels;

/// <summary>
/// The MainWindowViewModel is the ViewModel for the main window of the application.
/// <para>
/// It serves as the intermediary between the View and the Model
/// in the MVVM (Model-View-ViewModel) design pattern.
/// </para>
/// Handles data directly bound to the View.
/// </summary>
#pragma warning disable CA1822 // Mark members as static
public class MainWindowViewModel : ReactiveObject
{
    private const string SearchingLabel = "Search Results";
    private const long ShowDialogLimitB = 262144000; // 250 MiB
    private readonly string ThisPCLabel = FileSurferSettings.ThisPCLabel;
    private readonly string NewFileName = FileSurferSettings.NewFileName;
    private readonly string NewDirName = FileSurferSettings.NewDirectoryName;
    private readonly string NewImageName = FileSurferSettings.NewImageName + ".png";

    private readonly IFileIOHandler _fileIOHandler;
    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly IIconProvider _iconProvider;
    private readonly IShellHandler _shellHandler;
    private readonly IVersionControl _versionControl;
    private readonly UndoRedoHandler<IUndoableFileOperation> _undoRedoHistory;
    private readonly UndoRedoHandler<string> _pathHistory;
    private readonly ClipboardManager _clipboardManager;
    private readonly DispatcherTimer? _refreshTimer;

    private bool _isActionUserInvoked = true;
    private bool SortReversed
    {
        get => FileSurferSettings.SortReversed;
        set => FileSurferSettings.SortReversed = value;
    }
    private SortBy SortBy
    {
        get => FileSurferSettings.DefaultSort;
        set => FileSurferSettings.DefaultSort = value;
    }
    private CancellationTokenSource _searchCTS = new();
    private DateTime _lastModified;

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
    public ObservableCollection<FileSystemEntryViewModel> QuickAccess { get; } = new();

    /// <summary>
    /// Holds the Special Folders <see cref="FileSystemEntryViewModel"/>s.
    /// </summary>
    public FileSystemEntryViewModel[] SpecialFolders { get; }

    /// <summary>
    /// Holds the Drives <see cref="FileSystemEntryViewModel"/>s.
    /// </summary>
    public FileSystemEntryViewModel[] Drives { get; }

    /// <summary>
    /// Holds the path to the current directory displayed in FileSurfer.
    /// <para>
    /// Setting this property triggers a reload.
    /// Also adds the directory to <see cref="_pathHistory"/>,
    /// if the action was triggered by the user.
    /// </para>
    /// </summary>
    public string CurrentDir
    {
        get => _currentDir;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentDir, value);
            if (IsValidDirectory(value))
            {
                if (FileSurferSettings.OpenInLastLocation)
                    FileSurferSettings.OpenIn = value;

                if (Searching)
                    CancelSearch(value);

                Reload();

                if (_isActionUserInvoked && value != _pathHistory.Current)
                    _pathHistory.AddNewNode(value);
            }
            if (Directory.Exists(value))
                _lastModified = Directory.GetLastWriteTime(value);
        }
    }
    private string _currentDir = string.Empty;

    /// <summary>
    /// Indicates whether the current directory contains files or directories.
    /// </summary>
    public bool DirectoryEmpty
    {
        get => _directoryEmpty;
        set => this.RaiseAndSetIfChanged(ref _directoryEmpty, value);
    }
    private bool _directoryEmpty;

    /// <summary>
    /// Text that will be displayed in the Search bar.
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
        set => this.RaiseAndSetIfChanged(ref _searching, value);
    }
    private bool _searching;

    /// <summary>
    /// Contains a list of current repository's branches, displayed in the "Branches" combobox.
    /// </summary>
    public ObservableCollection<string> Branches { get; } = new();

    /// <summary>
    /// Currently selected branch in the "Branches" combobox.
    /// </summary>
    public string? CurrentBranch
    {
        get => _currentBranch;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentBranch, value);
            if (_isActionUserInvoked && !string.IsNullOrEmpty(value) && Branches.Contains(value))
            {
                if (!_versionControl.SwitchBranches(value, out string? errorMessage))
                    Reload();

                ForwardError(errorMessage);
            }
        }
    }
    private string? _currentBranch;

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
    /// Invokes <see cref="GoBack"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> GoBackCommand { get; }

    /// <summary>
    /// Invokes <see cref="GoForward"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> GoForwardCommand { get; }

    /// <summary>
    /// Invokes <see cref="GoUp"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> GoUpCommand { get; }

    /// <summary>
    /// Invokes <see cref="Reload"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ReloadCommand { get; }

    /// <summary>
    /// Invokes <see cref="OpenPowerShell"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> OpenPowerShellCommand { get; }

    /// <summary>
    /// Invokes <see cref="CancelSearch"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> CancelSearchCommand { get; }

    /// <summary>
    /// Invokes <see cref="NewFile"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> NewFileCommand { get; }

    /// <summary>
    /// Invokes <see cref="NewDir"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> NewDirCommand { get; }

    /// <summary>
    /// Invokes <see cref="Cut"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> CutCommand { get; }

    /// <summary>
    /// Invokes <see cref="Copy"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> CopyCommand { get; }

    /// <summary>
    /// Invokes <see cref="Paste"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> PasteCommand { get; }

    /// <summary>
    /// Invokes <see cref="MoveToTrash"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> MoveToTrashCommand { get; }

    /// <summary>
    /// Invokes <see cref="Delete"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    /// <summary>
    /// Invokes <see cref="SortByName"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SortByNameCommand { get; }

    /// <summary>
    /// Invokes <see cref="SortByDate"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SortByDateCommand { get; }

    /// <summary>
    /// Invokes <see cref="SortByType"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SortByTypeCommand { get; }

    /// <summary>
    /// Invokes <see cref="SortBySize"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SortBySizeCommand { get; }

    /// <summary>
    /// Invokes <see cref="Undo"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }

    /// <summary>
    /// Invokes <see cref="Redo"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }

    /// <summary>
    /// Invokes <see cref="SelectAll"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }

    /// <summary>
    /// Invokes <see cref="SelectNone"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SelectNoneCommand { get; }

    /// <summary>
    /// Invokes <see cref="InvertSelection"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> InvertSelectionCommand { get; }

    /// <summary>
    /// Invokes <see cref="Pull"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> PullCommand { get; }

    /// <summary>
    /// Invokes <see cref="Push"/>.
    /// </summary>
    public ReactiveCommand<Unit, Unit> PushCommand { get; }

    /// <summary>
    /// Initializes a new <see cref="MainWindowViewModel"/>.
    /// </summary>
    public MainWindowViewModel()
    {
        _fileInfoProvider = new WindowsFileInfoProvider();
        _shellHandler = new WindowsShellHandler();
        _fileIOHandler = new WindowsFileIOHandler(_fileInfoProvider, ShowDialogLimitB);
        _versionControl = new GitVersionControl(_shellHandler);
        _iconProvider = new IconProvider(_fileInfoProvider);
        _clipboardManager = new ClipboardManager(_fileIOHandler, NewImageName);
        _undoRedoHistory = new UndoRedoHandler<IUndoableFileOperation>();
        _pathHistory = new UndoRedoHandler<string>();
        SelectedFiles.CollectionChanged += UpdateSelectionInfo;
        GoBackCommand = ReactiveCommand.Create(GoBack);
        GoForwardCommand = ReactiveCommand.Create(GoForward);
        GoUpCommand = ReactiveCommand.Create(GoUp);
        ReloadCommand = ReactiveCommand.Create(Reload);
        OpenPowerShellCommand = ReactiveCommand.Create(OpenPowerShell);
        CancelSearchCommand = ReactiveCommand.Create(CancelSearch);
        NewFileCommand = ReactiveCommand.Create(NewFile);
        NewDirCommand = ReactiveCommand.Create(NewDir);
        CutCommand = ReactiveCommand.Create(Cut);
        CopyCommand = ReactiveCommand.Create(Copy);
        PasteCommand = ReactiveCommand.Create(Paste);
        MoveToTrashCommand = ReactiveCommand.Create(MoveToTrash);
        DeleteCommand = ReactiveCommand.Create(Delete);
        SortByNameCommand = ReactiveCommand.Create(SortByName);
        SortByDateCommand = ReactiveCommand.Create(SortByDate);
        SortByTypeCommand = ReactiveCommand.Create(SortByType);
        SortBySizeCommand = ReactiveCommand.Create(SortBySize);
        UndoCommand = ReactiveCommand.Create(Undo);
        RedoCommand = ReactiveCommand.Create(Redo);
        SelectAllCommand = ReactiveCommand.Create(SelectAll);
        SelectNoneCommand = ReactiveCommand.Create(SelectNone);
        InvertSelectionCommand = ReactiveCommand.Create(InvertSelection);
        PullCommand = ReactiveCommand.Create(Pull);
        PushCommand = ReactiveCommand.Create(Push);

        LoadQuickAccess();
        Drives = GetDrives();
        SpecialFolders = FileSurferSettings.ShowSpecialFolders
            ? GetSpecialFolders()
            : Array.Empty<FileSystemEntryViewModel>();

        CurrentDir = IsValidDirectory(FileSurferSettings.OpenIn)
            ? FileSurferSettings.OpenIn
            : ThisPCLabel;
        _pathHistory.AddNewNode(CurrentDir);

        if (FileSurferSettings.AutomaticRefresh)
        {
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(FileSurferSettings.AutomaticRefreshInterval),
            };
            _lastModified = Directory.GetLastWriteTime(CurrentDir);
            _refreshTimer.Tick += CheckForUpdates;
            _refreshTimer.Start();
        }
    }

    /// <summary>
    /// Compares <see cref="_lastModified"/> to the latest <see cref="Directory.GetLastWriteTime(string)"/>.
    /// <para>
    /// Invokes <see cref="Reload"/> if <see cref="Directory.GetLastWriteTime(string)"/> is newer.
    /// </para>
    /// </summary>
    private void CheckForUpdates(object? sender, EventArgs e)
    {
        if (!Directory.Exists(CurrentDir))
            return;

        DateTime latestModified = Directory.GetLastWriteTime(CurrentDir);
        if (latestModified > _lastModified)
        {
            _lastModified = latestModified;
            Reload();
        }
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
    private void Reload()
    {
        if (Searching)
            return;

        CheckVersionControl();
        if (CurrentDir == ThisPCLabel)
        {
            ShowDrives();
            SetDriveInfo();
        }
        else
        {
            LoadDirEntries();
            UpdateSelectionInfo();
        }
        CheckDirectoryEmpty();
        SetSearchWaterMark();

        if (FileSurferSettings.AutomaticRefresh)
            UpdateLastModified();
    }

    /// <summary>
    /// Opens a new <see cref="Views.ErrorWindow"/> dialog.
    /// </summary>
    private void ForwardError(string? errorMessage)
    {
        if (!string.IsNullOrEmpty(errorMessage))
            Dispatcher.UIThread.Post(() =>
            {
                new Views.ErrorWindow { ErrorMessage = errorMessage }.Show();
            });
    }

    /// <summary>
    /// Updates <see cref="_lastModified"/> to suppress unnecessary automatic reloads.
    /// </summary>
    private void UpdateLastModified() => _lastModified = DateTime.Now;

    /// <summary>
    /// Used for setting the text selection when renaming files.
    /// </summary>
    /// <returns>The length of the file name without extension.</returns>
    public int GetNameEndIndex(FileSystemEntryViewModel entry) =>
        SelectedFiles.Count > 0 ? Path.GetFileNameWithoutExtension(entry.PathToEntry).Length : 0;

    private bool IsValidDirectory(string path) =>
        path == ThisPCLabel || (!string.IsNullOrEmpty(path) && Directory.Exists(path));

    private void SetSearchWaterMark()
    {
        string? dirName = Path.GetFileName(CurrentDir);
        dirName = string.IsNullOrEmpty(dirName) ? Path.GetPathRoot(CurrentDir) : dirName;
        SearchWaterMark = $"Search {dirName}";
    }

    private void SetDriveInfo() =>
        SelectionInfo = Drives.Length == 1 ? "1 drive" : $"{FileEntries.Count} drives";

    /// <summary>
    /// Update <see cref="SelectionInfo"/> based on the current directory and selection in <see cref="SelectedFiles"/>.
    /// </summary>
    private void UpdateSelectionInfo(
        object? sender = null,
        NotifyCollectionChangedEventArgs? e = null
    )
    {
        if (CurrentDir == ThisPCLabel)
            return;

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
    public void OpenEntry(FileSystemEntryViewModel entry)
    {
        if (entry.IsDirectory)
            CurrentDir = entry.PathToEntry;
        else if (_fileInfoProvider.IsLinkedToDirectory(entry.PathToEntry, out string? directory))
        {
            if (directory is not null)
                CurrentDir = directory;
        }
        else
        {
            _shellHandler.OpenFile(entry.PathToEntry, out string? errorMessage);
            ForwardError(errorMessage);
        }
    }

    /// <summary>
    /// Shows the "Open file with" dialog.
    /// </summary>
    public void OpenAs(FileSystemEntryViewModel entry)
    {
        if (!entry.IsDirectory)
        {
            WindowsFileProperties.ShowOpenAsDialog(entry.PathToEntry, out string? errorMessage);
            ForwardError(errorMessage);
        }
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
                    || _fileInfoProvider.IsLinkedToDirectory(entry.PathToEntry, out _)
                )
                    return;
        }
        // To prevent collection changing during iteration
        foreach (FileSystemEntryViewModel entry in SelectedFiles.ToArray())
            OpenEntry(entry);
    }

    /// <summary>
    /// Opens the selected files in the notepad app specified in <see cref="FileSurferSettings.NotepadApp"/>
    /// </summary>
    public void OpenInNotepad()
    {
        foreach (FileSystemEntryViewModel entry in SelectedFiles)
        {
            if (!entry.IsDirectory)
            {
                _shellHandler.OpenInNotepad(entry.PathToEntry, out string? errorMessage);
                ForwardError(errorMessage);
            }
        }
    }

    /// <summary>
    /// Adds the selected <see cref="FileSystemEntryViewModel"/> to Quick Access.
    /// </summary>
    public void AddToQuickAccess(FileSystemEntryViewModel entry) => QuickAccess.Add(entry);

    /// <summary>
    /// Moves the <see cref="FileSystemEntryViewModel"/> under the index up within the Quick Access list.
    /// </summary>
    public void MoveUp(int i)
    {
        if (i > 0)
            (QuickAccess[i - 1], QuickAccess[i]) = (QuickAccess[i], QuickAccess[i - 1]);
    }

    /// <summary>
    /// Moves the <see cref="FileSystemEntryViewModel"/> under the index down within the Quick Access list.
    /// </summary>
    public void MoveDown(int i)
    {
        if (i < QuickAccess.Count - 1)
            (QuickAccess[i], QuickAccess[i + 1]) = (QuickAccess[i + 1], QuickAccess[i]);
    }

    /// <summary>
    /// Removes the <see cref="FileSystemEntryViewModel"/> under the index from the Quick Access list.
    /// </summary>
    public void RemoveFromQuickAccess(int index) => QuickAccess.RemoveAt(index);

    /// <summary>
    /// Opens the location of the selected entry during searching.
    /// </summary>
    public void OpenEntryLocation(FileSystemEntryViewModel entry) =>
        CurrentDir = Path.GetDirectoryName(entry.PathToEntry) ?? ThisPCLabel;

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
        if (Path.GetDirectoryName(CurrentDir) is not string parentDir)
            CurrentDir = ThisPCLabel;
        else if (CurrentDir != ThisPCLabel)
            CurrentDir = parentDir;
    }

    private FileSystemEntryViewModel[] GetDrives() =>
        _fileInfoProvider
            .GetDrives()
            .Select(driveInfo => new FileSystemEntryViewModel(_iconProvider, driveInfo))
            .ToArray();

    private FileSystemEntryViewModel[] GetSpecialFolders() =>
        _fileInfoProvider
            .GetSpecialFolders()
            .Where(dirPath => !string.IsNullOrEmpty(dirPath))
            .Select(dirPath => new FileSystemEntryViewModel(
                _fileInfoProvider,
                _iconProvider,
                dirPath,
                true
            ))
            .ToArray();

    private void LoadQuickAccess()
    {
        foreach (string path in FileSurferSettings.QuickAccess)
        {
            if (Directory.Exists(path))
                QuickAccess.Add(
                    new FileSystemEntryViewModel(_fileInfoProvider, _iconProvider, path, true)
                );
            else if (File.Exists(path))
                QuickAccess.Add(
                    new FileSystemEntryViewModel(_fileInfoProvider, _iconProvider, path, false)
                );
        }
    }

    private void ShowDrives()
    {
        FileEntries.Clear();
        foreach (FileSystemEntryViewModel drive in Drives)
            FileEntries.Add(drive);
    }

    private void LoadDirEntries()
    {
        string[] dirPaths = _fileInfoProvider.GetPathDirs(
            CurrentDir,
            FileSurferSettings.ShowHiddenFiles,
            FileSurferSettings.ShowProtectedFiles
        );
        string[] filePaths = _fileInfoProvider.GetPathFiles(
            CurrentDir,
            FileSurferSettings.ShowHiddenFiles,
            FileSurferSettings.ShowProtectedFiles
        );
        if (FileSurferSettings.TreatDotFilesAsHidden && !FileSurferSettings.ShowHiddenFiles)
            RemoveDotFiles(ref dirPaths, ref filePaths);

        FileSystemEntryViewModel[] directories = new FileSystemEntryViewModel[dirPaths.Length];
        FileSystemEntryViewModel[] files = new FileSystemEntryViewModel[filePaths.Length];

        for (int i = 0; i < dirPaths.Length; i++)
            directories[i] = new FileSystemEntryViewModel(
                _fileInfoProvider,
                _iconProvider,
                dirPaths[i],
                true,
                GetVCStatus(dirPaths[i])
            );

        for (int i = 0; i < filePaths.Length; i++)
            files[i] = new FileSystemEntryViewModel(
                _fileInfoProvider,
                _iconProvider,
                filePaths[i],
                false,
                GetVCStatus(filePaths[i])
            );

        AddEntries(directories, files);
    }

    private VCStatus GetVCStatus(string path) =>
        IsVersionControlled ? _versionControl.GetStatus(path) : VCStatus.NotVersionControlled;

    private void RemoveDotFiles(ref string[] dirPaths, ref string[] filePaths)
    {
        dirPaths = dirPaths.Where(path => !Path.GetFileName(path).StartsWith('.')).ToArray();
        filePaths = filePaths.Where(path => !Path.GetFileName(path).StartsWith('.')).ToArray();
    }

    /// <summary>
    /// Adds directories and files to <see cref="FileEntries"/>.
    /// </summary>
    private void AddEntries(
        FileSystemEntryViewModel[] directories,
        FileSystemEntryViewModel[] files
    )
    {
        if (SortBy is not SortBy.Name)
            SortInPlace(files, SortBy);

        if (SortBy is not SortBy.Name and not SortBy.Type and not SortBy.Size)
            SortInPlace(directories, SortBy);

        FileEntries.Clear();

        if (!SortReversed || SortBy is SortBy.Type or SortBy.Size)
            for (int i = 0; i < directories.Length; i++)
                FileEntries.Add(directories[i]);
        else
            for (int i = directories.Length - 1; i >= 0; i--)
                FileEntries.Add(directories[i]);

        if (!SortReversed)
            for (int i = 0; i < files.Length; i++)
                FileEntries.Add(files[i]);
        else
            for (int i = files.Length - 1; i >= 0; i--)
                FileEntries.Add(files[i]);
    }

    /// <summary>
    /// Sorts given array of <see cref="FileSystemEntryViewModel"/>s based on <see cref="SortBy"/>
    /// </summary>
    private void SortInPlace(FileSystemEntryViewModel[] entries, SortBy sortBy)
    {
        switch (sortBy)
        {
            case SortBy.Name:
                Array.Sort(entries, (x, y) => string.CompareOrdinal(x.Name, y.Name));
                break;

            case SortBy.Date:
                Array.Sort(entries, (x, y) => DateTime.Compare(y.LastModTime, x.LastModTime));
                break;

            case SortBy.Type:
                Array.Sort(entries, (x, y) => string.CompareOrdinal(x.Type, y.Type));
                break;

            case SortBy.Size:
                Array.Sort(entries, (x, y) => (y.SizeB ?? 0).CompareTo(x.SizeB ?? 0));
                break;

            default:
                throw new ArgumentException($"Unsupported sort option: {sortBy}", nameof(sortBy));
        }
    }

    /// <summary>
    /// Sets <see cref="IsVersionControlled"/> and updates <see cref="Branches"/>.
    /// </summary>
    private void CheckVersionControl()
    {
        IsVersionControlled =
            FileSurferSettings.GitIntegration
            && Directory.Exists(CurrentDir)
            && _versionControl.InitIfVersionControlled(CurrentDir);

        Branches.Clear();
        if (IsVersionControlled)
        {
            foreach (string branch in _versionControl.GetBranches())
                Branches.Add(branch);

            _isActionUserInvoked = false;
            CurrentBranch = _versionControl.GetCurrentBranchName();
            _isActionUserInvoked = true;
        }
    }

    /// <summary>
    /// Sets <see cref="DirectoryEmpty"/>.
    /// </summary>
    private void CheckDirectoryEmpty() => DirectoryEmpty = FileEntries.Count == 0;

    /// <summary>
    /// Sets the current directory to the previous directory in <see cref="_pathHistory"/>.
    /// <para>
    /// Removes the directory from <see cref="_pathHistory"/> in case it's invalid.
    /// </para>
    /// </summary>
    public void GoBack()
    {
        if (Searching)
            CancelSearch();

        if (_pathHistory.GetPrevious() is not string previousPath)
            return;

        _pathHistory.MoveToPrevious();

        if (IsValidDirectory(previousPath))
        {
            _isActionUserInvoked = false;
            CurrentDir = previousPath;
            _isActionUserInvoked = true;
        }
        else
            _pathHistory.RemoveNode(false);
    }

    /// <summary>
    /// Sets the current directory to the next directory in <see cref="_pathHistory"/>.
    /// <para>
    /// Removes the directory from <see cref="_pathHistory"/> in case it's invalid.
    /// </para>
    /// </summary>
    public void GoForward()
    {
        if (Searching)
            CancelSearch();

        if (_pathHistory.GetNext() is not string nextPath)
            return;

        _pathHistory.MoveToNext();

        if (IsValidDirectory(nextPath))
        {
            _isActionUserInvoked = false;
            CurrentDir = nextPath;
            _isActionUserInvoked = true;
        }
        else
            _pathHistory.RemoveNode(true);
    }

    /// <summary>
    /// Opens power-shell in <see cref="CurrentDir"/> if possible.
    /// </summary>
    private void OpenPowerShell()
    {
        if (CurrentDir == ThisPCLabel || Searching)
            return;

        _shellHandler.OpenCmdAt(CurrentDir, out string? errorMessage);
        ForwardError(errorMessage);
    }

    /// <summary>
    /// Prepares the <see cref="_searchCTS"/> cancellation token, updates <see cref="CurrentDir"/>, and starts the search.
    /// </summary>
    public async Task SearchAsync(string searchQuery)
    {
        if (Searching)
            _searchCTS.Cancel();

        if (_searchCTS.IsCancellationRequested)
        {
            CancellationTokenSource oldCTS = _searchCTS;
            _searchCTS = new CancellationTokenSource();
            oldCTS.Dispose();
        }
        string currentDir = _pathHistory.Current ?? ThisPCLabel;
        CurrentDir = SearchingLabel;
        FileEntries.Clear();
        Searching = true;

        if (currentDir != ThisPCLabel)
            await SearchDirectoryAsync(currentDir, searchQuery, _searchCTS.Token);
        else
            foreach (DriveInfo drive in _fileInfoProvider.GetDrives())
                await SearchDirectoryAsync(drive.Name, searchQuery, _searchCTS.Token);
    }

    /// <summary>
    /// Recursively searches <see cref="CurrentDir"/> and asynchronously
    /// adds matching <see cref="FileSystemEntryViewModel"/>s to <see cref="FileEntries"/>.
    /// </summary>
    private async Task SearchDirectoryAsync(
        string directory,
        string searchQuery,
        CancellationToken searchCTS
    )
    {
        Queue<string> directories = new();
        directories.Enqueue(directory);

        while (directories.Count > 0 && !searchCTS.IsCancellationRequested)
        {
            string currentDirPath = directories.Dequeue();

            IEnumerable<string> dirPaths = GetAllDirs(currentDirPath);
            Task<List<FileSystemEntryViewModel>> filesTask = Task.Run(
                () => GetFiles(currentDirPath, searchQuery)
            );
            Task<List<FileSystemEntryViewModel>> filteredDirsTask = Task.Run(
                () => GetDirs(dirPaths, searchQuery)
            );
            await Task.WhenAll(filesTask, filteredDirsTask);

            Dispatcher.UIThread.Post(() =>
            {
                foreach (FileSystemEntryViewModel file in filesTask.Result)
                    if (!searchCTS.IsCancellationRequested)
                        FileEntries.Add(file);

                foreach (FileSystemEntryViewModel dir in filteredDirsTask.Result)
                    if (!searchCTS.IsCancellationRequested)
                        FileEntries.Add(dir);
            });

            foreach (string dirPath in dirPaths)
                directories.Enqueue(dirPath);
        }
    }

    private List<FileSystemEntryViewModel> GetFiles(string directory, string query)
    {
        IEnumerable<string> filePaths = _fileInfoProvider.GetPathFiles(
            directory,
            FileSurferSettings.ShowHiddenFiles,
            FileSurferSettings.ShowProtectedFiles
        );
        if (!FileSurferSettings.ShowHiddenFiles && FileSurferSettings.TreatDotFilesAsHidden)
            filePaths = filePaths.Where(path => !Path.GetFileName(path).StartsWith('.'));

        return FilterPaths(filePaths, query)
            .Select(path => new FileSystemEntryViewModel(
                _fileInfoProvider,
                _iconProvider,
                path,
                false,
                GetVCStatus(path)
            ))
            .ToList();
    }

    private IEnumerable<string> GetAllDirs(string directory)
    {
        IEnumerable<string> dirPaths = _fileInfoProvider.GetPathDirs(
            directory,
            FileSurferSettings.ShowHiddenFiles,
            FileSurferSettings.ShowProtectedFiles
        );
        if (!FileSurferSettings.ShowHiddenFiles && FileSurferSettings.TreatDotFilesAsHidden)
            return dirPaths.Where(path => !Path.GetFileName(path).StartsWith('.'));

        return dirPaths;
    }

    private List<FileSystemEntryViewModel> GetDirs(IEnumerable<string> dirs, string query) =>
        FilterPaths(dirs, query)
            .Select(path => new FileSystemEntryViewModel(
                _fileInfoProvider,
                _iconProvider,
                path,
                true,
                GetVCStatus(path)
            ))
            .ToList();

    private IEnumerable<string> FilterPaths(IEnumerable<string> paths, string query) =>
        paths.Where(path =>
            Path.GetFileName(path).Contains(query, StringComparison.CurrentCultureIgnoreCase)
        );

    /// <summary>
    /// Cancels the search and sets <see cref="CurrentDir"/> to the current in <see cref="_pathHistory"/>.
    /// </summary>
    public void CancelSearch()
    {
        _searchCTS.Cancel();
        Searching = false;
        CurrentDir = _pathHistory.Current ?? ThisPCLabel;
    }

    /// <summary>
    /// Cancels the search and sets <see cref="CurrentDir"/> to the parameter.
    /// </summary>
    private void CancelSearch(string directory)
    {
        _searchCTS.Cancel();
        Searching = false;
        CurrentDir = directory;
    }

    /// <summary>
    /// Creates a new file using <see cref="_fileIOHandler"/> in <see cref="CurrentDir"/>
    /// with the name specified in <see cref="FileSurferSettings.NewFileName"/>.
    /// <para>
    /// Invokes <see cref="Reload"/> and adds a new <see cref="NewFileAt"/> operation
    /// to <see cref="_undoRedoHistory"/> if it was a success.
    /// </para>
    /// </summary>
    private void NewFile()
    {
        string newFileName = FileNameGenerator.GetAvailableName(CurrentDir, NewFileName);
        if (_fileIOHandler.NewFileAt(CurrentDir, newFileName, out string? errorMessage))
        {
            Reload();
            _undoRedoHistory.AddNewNode(new NewFileAt(_fileIOHandler, CurrentDir, newFileName));
            SelectedFiles.Add(FileEntries.First(entry => entry.Name == newFileName));
        }
        else
            ForwardError(errorMessage);
    }

    /// <summary>
    /// Creates a new directory using <see cref="_fileIOHandler"/> in <see cref="CurrentDir"/>
    /// with the name specified in <see cref="FileSurferSettings.NewDirectoryName"/>.
    /// <para>
    /// Invokes <see cref="Reload"/> and adds a new <see cref="NewDirAt"/> operation
    /// to <see cref="_undoRedoHistory"/> if it was a success.
    /// </para>
    /// </summary>
    private void NewDir()
    {
        string newDirName = FileNameGenerator.GetAvailableName(CurrentDir, NewDirName);
        if (_fileIOHandler.NewDirAt(CurrentDir, newDirName, out string? errorMessage))
        {
            Reload();
            _undoRedoHistory.AddNewNode(new NewDirAt(_fileIOHandler, CurrentDir, newDirName));
            SelectedFiles.Add(FileEntries.First(entry => entry.Name == newDirName));
        }
        else
            ForwardError(errorMessage);
    }

    /// <summary>
    /// Creates an archive from the <see cref="FileSystemEntryViewModel"/>s in <see cref="SelectedFiles"/>.
    /// </summary>
    public async void AddToArchive()
    {
        if (!Directory.Exists(CurrentDir))
            return;

        string fileName = Path.GetFileNameWithoutExtension(SelectedFiles[^1].Name) + ".zip";
        await ZipFilesWrapperAsync(fileName);
        Reload();
    }

    private Task ZipFilesWrapperAsync(string fileName) =>
        Task.Run(() =>
        {
            ArchiveManager.ZipFiles(
                SelectedFiles.ToArray(),
                CurrentDir,
                FileNameGenerator.GetAvailableName(CurrentDir, fileName),
                out string? errorMessage
            );
            ForwardError(errorMessage);
            return;
        });

    /// <summary>
    /// Extracts the archives selected in <see cref="SelectedFiles"/>.
    /// </summary>
    public async void ExtractArchive()
    {
        if (!Directory.Exists(CurrentDir))
            return;

        foreach (FileSystemEntryViewModel entry in SelectedFiles)
            if (!ArchiveManager.IsZipped(entry.PathToEntry))
            {
                ForwardError($"Entry \"{entry.Name}\" is not an archive.");
                return;
            }

        await ExtractArchiveWrapperAsync(
            SelectedFiles.Select(entry => entry.PathToEntry).ToArray()
        );
        Reload();
    }

    private Task ExtractArchiveWrapperAsync(string[] archives) =>
        Task.Run(() =>
        {
            foreach (string path in archives)
            {
                ArchiveManager.UnzipArchive(path, CurrentDir, out string? errorMessage);
                ForwardError(errorMessage);
            }
            return;
        });

    /// <summary>
    /// Copies the path to the selected <see cref="FileSystemEntryViewModel"/> to the system clipboard.
    /// </summary>
    public void CopyPath(FileSystemEntryViewModel entry) =>
        _clipboardManager.CopyPathToFile(entry.PathToEntry);

    /// <summary>
    /// Relays the current selection in <see cref="SelectedFiles"/> to <see cref="_clipboardManager"/>.
    /// </summary>
    public void Cut()
    {
        _clipboardManager.Cut(SelectedFiles.ToList(), CurrentDir, out string? errorMessage);
        ForwardError(errorMessage);
    }

    /// <summary>
    /// Relays the current selection in <see cref="SelectedFiles"/> to <see cref="_clipboardManager"/>.
    /// </summary>
    public void Copy()
    {
        _clipboardManager.Copy(SelectedFiles.ToList(), CurrentDir, out string? errorMessage);
        ForwardError(errorMessage);
    }

    /// <summary>
    /// Determines the type of paste operation and executes it using <see cref="_clipboardManager"/>.
    /// <para>
    /// Adds the appropriate <see cref="IUndoableFileOperation"/> to <see cref="_pathHistory"/> if the operation was a success.
    /// </para>
    /// Invokes <see cref="Reload()"/>.
    /// </summary>
    private void Paste()
    {
        string? errorMessage;
        if (_clipboardManager.IsDuplicateOperation(CurrentDir))
        {
            if (_clipboardManager.Duplicate(CurrentDir, out string[] copyNames, out errorMessage))
                _undoRedoHistory.AddNewNode(
                    new DuplicateFiles(_fileIOHandler, _clipboardManager.GetClipboard(), copyNames)
                );
        }
        else if (_clipboardManager.IsCutOperation)
        {
            FileSystemEntryViewModel[] clipBoard = _clipboardManager.GetClipboard();
            if (_clipboardManager.Paste(CurrentDir, out errorMessage))
                _undoRedoHistory.AddNewNode(new MoveFilesTo(_fileIOHandler, clipBoard, CurrentDir));
        }
        else
        {
            if (_clipboardManager.Paste(CurrentDir, out errorMessage))
                _undoRedoHistory.AddNewNode(
                    new CopyFilesTo(_fileIOHandler, _clipboardManager.GetClipboard(), CurrentDir)
                );
        }
        ForwardError(errorMessage);
        Reload();
    }

    /// <summary>
    /// Relays the operation to <see cref="_fileIOHandler"/> and invokes <see cref="Reload"/>.
    /// </summary>
    public void CreateShortcut(FileSystemEntryViewModel entry)
    {
        _shellHandler.CreateLink(entry.PathToEntry, out string? errorMessage);
        ForwardError(errorMessage);
        Reload();
    }

    /// <summary>
    /// Relays the operation to <see cref="_fileIOHandler"/>.
    /// </summary>
    public void ShowProperties(FileSystemEntryViewModel entry)
    {
        WindowsFileProperties.ShowFileProperties(entry.PathToEntry, out string? errorMessage);
        ForwardError(errorMessage);
    }

    /// <summary>
    /// Relays the operation to <see cref="RenameOne(string)"/> or <see cref="RenameMultiple(string)"/>.
    /// </summary>
    public void Rename(string newName)
    {
        if (SelectedFiles.Count == 1)
            RenameOne(newName);
        else if (SelectedFiles.Count > 1)
            RenameMultiple(newName);
    }

    private void RenameOne(string newName)
    {
        FileSystemEntryViewModel entry = SelectedFiles[0];
        bool result = entry.IsDirectory
            ? _fileIOHandler.RenameDirAt(entry.PathToEntry, newName, out string? errorMessage)
            : _fileIOHandler.RenameFileAt(entry.PathToEntry, newName, out errorMessage);

        if (result)
        {
            _undoRedoHistory.AddNewNode(new RenameOne(_fileIOHandler, entry, newName));
            Reload();
            SelectedFiles.Add(FileEntries.First(e => e.Name == newName));
        }
        else
            ForwardError(errorMessage);
    }

    private void RenameMultiple(string namingPattern)
    {
        bool onlyFiles = !SelectedFiles[0].IsDirectory;
        string extension = onlyFiles
            ? Path.GetExtension(SelectedFiles[0].PathToEntry)
            : string.Empty;

        if (!FileNameGenerator.CanBeRenamedCollectively(SelectedFiles, onlyFiles, extension))
        {
            ForwardError("Selected entries aren't of the same type.");
            return;
        }

        string[] newNames = FileNameGenerator.GetAvailableNames(SelectedFiles, namingPattern);
        bool errorOccured = false;
        for (int i = 0; i < SelectedFiles.Count; i++)
        {
            string path = SelectedFiles[i].PathToEntry;
            bool result = onlyFiles
                ? _fileIOHandler.RenameFileAt(path, newNames[i], out string? errorMessage)
                : _fileIOHandler.RenameDirAt(path, newNames[i], out errorMessage);

            errorOccured = !result || errorOccured;
            if (!result)
                ForwardError(errorMessage);
        }
        if (!errorOccured)
            _undoRedoHistory.AddNewNode(
                new RenameMultiple(_fileIOHandler, SelectedFiles.ToArray(), newNames)
            );
        Reload();
    }

    /// <summary>
    /// Moves the <see cref="FileSystemEntryViewModel"/>s in <see cref="SelectedFiles"/> to the system trash using <see cref="_fileIOHandler"/>.
    /// <para>
    /// Adds the operation to <see cref="_undoRedoHistory"/> if the operation was successful.
    /// </para>
    /// Invokes <see cref="Reload"/>.
    /// </summary>
    public void MoveToTrash()
    {
        bool errorOccured = false;
        foreach (FileSystemEntryViewModel entry in SelectedFiles)
        {
            bool result = entry.IsDirectory
                ? _fileIOHandler.MoveDirToTrash(entry.PathToEntry, out string? errorMessage)
                : _fileIOHandler.MoveFileToTrash(entry.PathToEntry, out errorMessage);

            errorOccured = !result || errorOccured;
            ForwardError(errorMessage);
        }
        if (!errorOccured)
            _undoRedoHistory.AddNewNode(
                new MoveFilesToTrash(_fileIOHandler, SelectedFiles.ToArray())
            );
        Reload();
    }

    public void FlattenFolder(FileSystemEntryViewModel entry)
    {
        if (!entry.IsDirectory)
        {
            ForwardError($"Cannot flatten a file: \"{entry.Name}\".");
            return;
        }

        FlattenFolder action = new(_fileIOHandler, _fileInfoProvider, entry.PathToEntry);
        if (action.Redo(out string? errorMessage))
            _undoRedoHistory.AddNewNode(action);
        else
            ForwardError(errorMessage);

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
        {
            string? errorMessage;
            if (entry.IsDirectory)
                _fileIOHandler.DeleteDir(entry.PathToEntry, out errorMessage);
            else
                _fileIOHandler.DeleteFile(entry.PathToEntry, out errorMessage);

            ForwardError(errorMessage);
        }
        Reload();
    }

    /// <summary>
    /// Sets <see cref="SortBy"/> to <see cref="SortBy.Name"/> and determines <see cref="SortReversed"/>.
    /// <para>
    /// Invokes <see cref="Reload"/>.
    /// </para>
    /// </summary>
    private void SortByName()
    {
        SortReversed = SortBy is SortBy.Name && !SortReversed;
        SortBy = SortBy.Name;
        Reload();
    }

    /// <summary>
    /// Sets <see cref="SortBy"/> to <see cref="SortBy.Date"/> and determines <see cref="SortReversed"/>.
    /// <para>
    /// Invokes <see cref="Reload"/>.
    /// </para>
    /// </summary>
    private void SortByDate()
    {
        SortReversed = SortBy is SortBy.Date && !SortReversed;
        SortBy = SortBy.Date;
        Reload();
    }

    /// <summary>
    /// Sets <see cref="SortBy"/> to <see cref="SortBy.Type"/> and determines <see cref="SortReversed"/>.
    /// <para>
    /// Invokes <see cref="Reload"/>.
    /// </para>
    /// </summary>
    private void SortByType()
    {
        SortReversed = SortBy is SortBy.Type && !SortReversed;
        SortBy = SortBy.Type;
        Reload();
    }

    /// <summary>
    /// Sets <see cref="SortBy"/> to <see cref="SortBy.Size"/> and determines <see cref="SortReversed"/>.
    /// <para>
    /// Invokes <see cref="Reload"/>.
    /// </para>
    /// </summary>
    private void SortBySize()
    {
        SortReversed = SortBy is SortBy.Size && !SortReversed;
        SortBy = SortBy.Size;
        Reload();
    }

    /// <summary>
    /// Invokes <see cref="IUndoableFileOperation.Undo(out string?)"/> on the current
    /// <see cref="IUndoableFileOperation"/> and goes back in <see cref="_undoRedoHistory"/>.
    /// </summary>
    private void Undo()
    {
        if (_undoRedoHistory.IsTail())
            _undoRedoHistory.MoveToPrevious();

        if (_undoRedoHistory.IsHead())
            return;

        IUndoableFileOperation operation =
            _undoRedoHistory.Current ?? throw new NullReferenceException();

        if (operation.Undo(out string? errorMessage))
        {
            _undoRedoHistory.MoveToPrevious();
            Reload();
        }
        else
        {
            if (FileSurferSettings.ShowUndoRedoErrorDialogs)
                ForwardError(errorMessage);

            _undoRedoHistory.RemoveNode(true);
        }
    }

    /// <summary>
    /// Moves forward in <see cref="_undoRedoHistory"/> and invokes
    /// <see cref="IUndoableFileOperation.Redo(out string?)"/> on the current <see cref="IUndoableFileOperation"/>.
    /// </summary>
    private void Redo()
    {
        _undoRedoHistory.MoveToNext();
        if (_undoRedoHistory.Current is null)
            return;

        IUndoableFileOperation operation = _undoRedoHistory.Current;
        if (operation.Redo(out string? errorMessage))
            Reload();
        else
        {
            if (FileSurferSettings.ShowUndoRedoErrorDialogs)
                ForwardError(errorMessage);

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
    /// Relays the operations to <see cref="_versionControl"/>.
    /// </summary>
    public void StageFile(FileSystemEntryViewModel entry)
    {
        if (IsVersionControlled)
        {
            _versionControl.StagePath(entry.PathToEntry, out string? errorMessage);
            ForwardError(errorMessage);
        }
    }

    /// <summary>
    /// Relays the operations to <see cref="_versionControl"/>.
    /// </summary>
    public void UnstageFile(FileSystemEntryViewModel entry)
    {
        if (IsVersionControlled)
        {
            _versionControl.UnstagePath(entry.PathToEntry, out string? errorMessage);
            ForwardError(errorMessage);
        }
    }

    /// <summary>
    /// Relays the operations to <see cref="_versionControl"/>.
    /// </summary>
    private void Pull()
    {
        if (IsVersionControlled)
        {
            if (_versionControl.DownloadChanges(out string? errorMessage))
                Reload();
            else
                ForwardError(errorMessage);
        }
    }

    /// <summary>
    /// Relays the operations to <see cref="_versionControl"/>.
    /// </summary>
    public void Commit(string commitMessage)
    {
        if (IsVersionControlled)
        {
            if (_versionControl.CommitChanges(commitMessage, out string? errorMessage))
                Reload();
            else
                ForwardError(errorMessage);
        }
    }

    /// <summary>
    /// Relays the operations to <see cref="_versionControl"/>.
    /// </summary>
    private void Push()
    {
        if (IsVersionControlled && !_versionControl.UploadChanges(out string? errorMessage))
            ForwardError(errorMessage);
    }

    /// <summary>
    /// Disposes <see cref="MainWindowViewModel"/> resources.
    /// </summary>
    public void DisposeResources()
    {
        _versionControl.Dispose();
        _searchCTS.Dispose();
        _refreshTimer?.Stop();
    }
}
#pragma warning restore CA1822 // Mark members as static
