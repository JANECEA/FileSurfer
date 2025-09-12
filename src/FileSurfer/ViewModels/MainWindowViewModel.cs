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
public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private const string SearchingFinishedLabel = "Searching finished";
    private static readonly IReadOnlyList<string> SearchingStates =
    [
        "Searching",
        "Searching.",
        "Searching..",
        "Searching...",
    ];
    private string ThisPCLabel => FileSurferSettings.ThisPCLabel;

    private readonly IFileIOHandler _fileIOHandler;
    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly IShellHandler _shellHandler;
    private readonly IVersionControl _versionControl;
    private readonly IClipboardManager _clipboardManager;
    private readonly FileSystemEntryVMFactory _entryVMFactory;
    private readonly UndoRedoHandler<IUndoableFileOperation> _undoRedoHistory;
    private readonly UndoRedoHandler<string> _pathHistory;
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
        set => this.RaiseAndSetIfChanged(ref _currentDir, value);
    }
    private string _currentDir = string.Empty;

    private void SetCurrentDirNoHistory(string dirPath)
    {
        if (Directory.Exists(dirPath))
        {
            dirPath = Path.GetFullPath(dirPath);
            _lastModified = Directory.GetLastWriteTime(dirPath);
        }
        else if (dirPath != ThisPCLabel && !Searching)
        {
            ForwardError($"Directory \"{dirPath}\" does not exist.");
            dirPath = _pathHistory.Current ?? GetClosestExistingParent(dirPath);
        }

        CurrentDir = dirPath;
        if (IsValidDirectory(dirPath))
        {
            if (FileSurferSettings.OpenInLastLocation)
                FileSurferSettings.OpenIn = dirPath;

            if (Searching)
                CancelSearch(dirPath);

            Reload();
        }
    }

    public void SetCurrentDir(string dirPath)
    {
        SetCurrentDirNoHistory(dirPath);

        if (IsValidDirectory(dirPath) && CurrentDir != _pathHistory.Current)
                _pathHistory.AddNewNode(dirPath);
    }

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
    public string CurrentBranch
    {
        get => _currentBranch;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentBranch, value);
            if (_isActionUserInvoked && !string.IsNullOrEmpty(value) && Branches.Contains(value))
            {
                ForwardIfError(_versionControl.SwitchBranches(value));
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
    /// Invokes <see cref="SetSortBy"/>.
    /// </summary>
    public ReactiveCommand<SortBy, Unit> SetSortByCommand { get; }

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
    public MainWindowViewModel(
        string initialDir,
        IFileIOHandler fileIOHandler,
        IFileInfoProvider fileInfoProvider,
        IShellHandler shellHandler,
        IVersionControl versionControl,
        IClipboardManager clipboardManager
    )
    {
        _fileIOHandler = fileIOHandler;
        _fileInfoProvider = fileInfoProvider;
        _shellHandler = shellHandler;
        _versionControl = versionControl;
        _clipboardManager = clipboardManager;
        _entryVMFactory = new FileSystemEntryVMFactory(
            _fileInfoProvider,
            new IconProvider(_fileInfoProvider)
        );
        _undoRedoHistory = new UndoRedoHandler<IUndoableFileOperation>();
        _pathHistory = new UndoRedoHandler<string>();
        SelectedFiles.CollectionChanged += UpdateSelectionInfo;
        FileEntries.CollectionChanged += UpdateSelectionInfo;

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
        SetSortByCommand = ReactiveCommand.Create<SortBy>(SetSortBy);
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

        SetCurrentDir(initialDir);

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
        else if (IsVersionControlled)
            Reload();
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
            ShowDrives();
        else if (Directory.Exists(CurrentDir))
            LoadDirEntries();
        else
        {
            ForwardError($"Directory: \"{CurrentDir}\" does not exist.");
            SetCurrentDir(GetClosestExistingParent(CurrentDir));
        }

        CheckDirectoryEmpty();
        SetSearchWaterMark();

        if (FileSurferSettings.AutomaticRefresh)
            UpdateLastModified();
    }

    private string GetClosestExistingParent(string dirPath)
    {
        string? path = dirPath;

        while (!Directory.Exists(path))
            if ((path = Path.GetDirectoryName(dirPath)) is null)
                return ThisPCLabel;

        return path;
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
    /// Opens a new <see cref="Views.ErrorWindow"/> dialog.
    /// </summary>
    private void ForwardIfError(IResult result)
    {
        if (result.IsOk)
            return;

        foreach (string errorMessage in result.Errors)
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
        SelectedFiles.Count > 0 ? entry.FileSystemEntry.NameWOExtension.Length : 0;

    private bool IsValidDirectory(string path) =>
        path == ThisPCLabel || (!string.IsNullOrEmpty(path) && Directory.Exists(path));

    private void SetSearchWaterMark()
    {
        string? dirName = Path.GetFileName(CurrentDir);
        dirName = string.IsNullOrEmpty(dirName) ? Path.GetPathRoot(CurrentDir) : dirName;
        SearchWaterMark = $"Search {dirName}";
    }

    /// <summary>
    /// Update <see cref="SelectionInfo"/> based on the current directory and selection in <see cref="SelectedFiles"/>.
    /// </summary>
    private void UpdateSelectionInfo(object? sender, NotifyCollectionChangedEventArgs? args)
    {
        if (CurrentDir == ThisPCLabel)
        {
            SelectionInfo = Drives.Length == 1 ? "1 drive" : $"{FileEntries.Count} drives";
            return;
        }

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
            SetCurrentDir(entry.PathToEntry);
        else if (_fileInfoProvider.IsLinkedToDirectory(entry.PathToEntry, out string? directory))
        {
            if (directory is not null)
                SetCurrentDir(directory);
        }
        else
            ForwardIfError(_shellHandler.OpenFile(entry.PathToEntry));
    }

    /// <summary>
    /// Shows the "Open file with" dialog.
    /// </summary>
    public void OpenAs(FileSystemEntryViewModel entry)
    {
        if (!entry.IsDirectory)
            ForwardIfError(WindowsFileProperties.ShowOpenAsDialog(entry.PathToEntry));
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
        foreach (FileSystemEntryViewModel entry in SelectedFiles.ConvertToArray())
            OpenEntry(entry);
    }

    /// <summary>
    /// Opens the selected files in the notepad app specified in <see cref="FileSurferSettings.NotepadApp"/>
    /// </summary>
    public void OpenInNotepad()
    {
        foreach (FileSystemEntryViewModel entry in SelectedFiles)
            if (!entry.IsDirectory)
                ForwardIfError(
                    _shellHandler.OpenInNotepad(entry.PathToEntry, FileSurferSettings.NotepadApp)
                );
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
        SetCurrentDir(Path.GetDirectoryName(entry.PathToEntry) ?? ThisPCLabel);

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
        if (Path.GetDirectoryName(CurrentDir.TrimEnd('\\')) is not string parentDir)
            SetCurrentDir(ThisPCLabel);
        else if (CurrentDir != ThisPCLabel)
            SetCurrentDir(parentDir);
    }

    private FileSystemEntryViewModel[] GetDrives() =>
        _fileInfoProvider.GetDrives().Select(_entryVMFactory.Drive).ToArray();

    private FileSystemEntryViewModel[] GetSpecialFolders() =>
        _fileInfoProvider
            .GetSpecialFolders()
            .Where(dirPath => !string.IsNullOrEmpty(dirPath))
            .Select(_entryVMFactory.Directory)
            .ToArray();

    private void LoadQuickAccess()
    {
        foreach (string path in FileSurferSettings.QuickAccess)
        {
            if (Directory.Exists(path))
                QuickAccess.Add(_entryVMFactory.Directory(path));
            else if (File.Exists(path))
                QuickAccess.Add(_entryVMFactory.File(path));
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
            directories[i] = _entryVMFactory.Directory(dirPaths[i], GetVCStatus(dirPaths[i]));

        for (int i = 0; i < filePaths.Length; i++)
            files[i] = _entryVMFactory.File(filePaths[i], GetVCStatus(filePaths[i]));

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

        LoadBranches();
    }

    /// <summary>
    /// Updates <see cref="Branches"/>.
    /// </summary>
    private void LoadBranches()
    {
        if (!IsVersionControlled)
        {
            Branches.Clear();
            return;
        }

        string currentBranch = _versionControl.GetCurrentBranchName();
        string[] branches = _versionControl.GetBranches();
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
            SetCurrentDirNoHistory(previousPath);
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
            SetCurrentDirNoHistory (nextPath);
        else
            _pathHistory.RemoveNode(true);
    }

    /// <summary>
    /// Opens power-shell in <see cref="CurrentDir"/> if possible.
    /// </summary>
    private void OpenPowerShell()
    {
        if (CurrentDir != ThisPCLabel && !Searching)
            ForwardIfError(_shellHandler.OpenCmdAt(CurrentDir));
    }

    /// <summary>
    /// Prepares the <see cref="_searchCTS"/> cancellation token, updates <see cref="CurrentDir"/>, and starts the search.
    /// </summary>
    public async Task SearchAsync(string searchQuery)
    {
        if (Searching)
            await _searchCTS.CancelAsync();

        if (_searchCTS.IsCancellationRequested)
        {
            CancellationTokenSource oldCTS = _searchCTS;
            _searchCTS = new CancellationTokenSource();
            oldCTS.Dispose();
        }
        string currentDir = _pathHistory.Current ?? ThisPCLabel;
        Searching = true;
        FileEntries.Clear();

        DispatcherTimer timer = StartAnimationTimer();

        if (currentDir != ThisPCLabel)
            await SearchDirectoryAsync(currentDir, searchQuery, _searchCTS.Token);
        else
            foreach (DriveInfo drive in _fileInfoProvider.GetDrives())
                await SearchDirectoryAsync(drive.Name, searchQuery, _searchCTS.Token);

        timer.Stop();
        if (!_searchCTS.IsCancellationRequested)
            CurrentDir = SearchingFinishedLabel;
    }

    private DispatcherTimer StartAnimationTimer()
    {
        int index = 0;
        DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            if (!_searchCTS.IsCancellationRequested)
                CurrentDir = SearchingStates[index = (index + 1) % SearchingStates.Count];
        };
        timer.Start();
        CurrentDir = SearchingStates[0];
        return timer;
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
                () => GetFiles(currentDirPath, searchQuery),
                searchCTS
            );
            Task<List<FileSystemEntryViewModel>> filteredDirsTask = Task.Run(
                () => GetDirs(dirPaths, searchQuery),
                searchCTS
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
            .Select(filePath => _entryVMFactory.File(filePath, GetVCStatus(filePath)))
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
            .Select(dirPath => _entryVMFactory.Directory(dirPath, GetVCStatus(dirPath)))
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
        SetCurrentDir(_pathHistory.Current ?? ThisPCLabel);
    }

    /// <summary>
    /// Cancels the search and sets <see cref="CurrentDir"/> to the parameter.
    /// </summary>
    private void CancelSearch(string directory)
    {
        _searchCTS.Cancel();
        Searching = false;
        SetCurrentDir(directory);
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
        string newFileName = FileNameGenerator.GetAvailableName(
            CurrentDir,
            FileSurferSettings.NewFileName
        );

        NewFileAt operation = new(_fileIOHandler, CurrentDir, newFileName);
        IResult result = operation.Invoke();
        if (result.IsOk)
        {
            Reload();
            _undoRedoHistory.AddNewNode(operation);
            SelectedFiles.Add(FileEntries.First(entry => entry.Name == newFileName));
        }
        ForwardIfError(result);
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
        string newDirName = FileNameGenerator.GetAvailableName(
            CurrentDir,
            FileSurferSettings.NewDirectoryName
        );

        NewDirAt operation = new(_fileIOHandler, CurrentDir, newDirName);
        IResult result = operation.Invoke();
        if (result.IsOk)
        {
            Reload();
            _undoRedoHistory.AddNewNode(operation);
            SelectedFiles.Add(FileEntries.First(entry => entry.Name == newDirName));
        }
        ForwardIfError(result);
    }

    /// <summary>
    /// Creates an archive from the <see cref="FileSystemEntryViewModel"/>s in <see cref="SelectedFiles"/>.
    /// </summary>
    public async Task AddToArchive()
    {
        if (!Directory.Exists(CurrentDir))
            return;

        string archiveName =
            SelectedFiles[^1].FileSystemEntry.NameWOExtension + ArchiveManager.ArchiveTypeExtension;
        await ZipFilesWrapperAsync(archiveName);
        Reload();
    }

    private Task ZipFilesWrapperAsync(string archiveName) =>
        Task.Run(
            () =>
                ForwardIfError(
                    ArchiveManager.ZipFiles(
                        SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry),
                        CurrentDir,
                        FileNameGenerator.GetAvailableName(CurrentDir, archiveName)
                    )
                )
        );

    private Task ExtractArchiveWrapperAsync(string[] archives) =>
        Task.Run(() =>
        {
            foreach (string path in archives)
                ForwardIfError(ArchiveManager.UnzipArchive(path, CurrentDir));
        });

    /// <summary>
    /// Extracts the archives selected in <see cref="SelectedFiles"/>.
    /// </summary>
    public async Task ExtractArchive()
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

    /// <summary>
    /// Copies the path to the selected <see cref="FileSystemEntryViewModel"/> to the system clipboard.
    /// </summary>
    public void CopyPath(FileSystemEntryViewModel entry) =>
        _clipboardManager.CopyPathToFile(entry.PathToEntry);

    /// <summary>
    /// Relays the current selection in <see cref="SelectedFiles"/> to <see cref="_clipboardManager"/>.
    /// </summary>
    public void Cut() =>
        ForwardIfError(
            _clipboardManager.Cut(
                SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry),
                CurrentDir
            )
        );

    /// <summary>
    /// Relays the current selection in <see cref="SelectedFiles"/> to <see cref="_clipboardManager"/>.
    /// </summary>
    public void Copy() =>
        ForwardIfError(
            _clipboardManager.Copy(
                SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry),
                CurrentDir
            )
        );

    /// <summary>
    /// Determines the type of paste operation and executes it using <see cref="_clipboardManager"/>.
    /// <para>
    /// Adds the appropriate <see cref="IUndoableFileOperation"/> to <see cref="_pathHistory"/> if the operation was a success.
    /// </para>
    /// Invokes <see cref="Reload()"/>.
    /// </summary>
    private void Paste()
    {
        IFileSystemEntry[] clipboard = _clipboardManager.GetClipboard();

        IResult result;
        IUndoableFileOperation operation;
        if (_clipboardManager.IsDuplicateOperation(CurrentDir))
        {
            result = _clipboardManager.Duplicate(CurrentDir, out string[] copyNames);
            operation = new DuplicateFiles(_fileIOHandler, clipboard, copyNames);
        }
        else if (_clipboardManager.IsCutOperation)
        {
            result = _clipboardManager.Paste(CurrentDir);
            operation = new MoveFilesTo(_fileIOHandler, clipboard, CurrentDir);
        }
        else
        {
            result = _clipboardManager.Paste(CurrentDir);
            operation = new CopyFilesTo(_fileIOHandler, clipboard, CurrentDir);
        }

        if (result.IsOk)
            _undoRedoHistory.AddNewNode(operation);
        ForwardIfError(result);
        Reload();
    }

    /// <summary>
    /// Relays the operation to <see cref="_fileIOHandler"/> and invokes <see cref="Reload"/>.
    /// </summary>
    public void CreateShortcut(FileSystemEntryViewModel entry)
    {
        ForwardIfError(_shellHandler.CreateLink(entry.PathToEntry));
        Reload();
    }

    /// <summary>
    /// Relays the operation to <see cref="_fileIOHandler"/>.
    /// </summary>
    public void ShowProperties(FileSystemEntryViewModel entry) =>
        ForwardIfError(WindowsFileProperties.ShowFileProperties(entry.PathToEntry));

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

        RenameOne operation = new(_fileIOHandler, entry.FileSystemEntry, newName);
        IResult result = operation.Invoke();
        if (result.IsOk)
        {
            _undoRedoHistory.AddNewNode(operation);
            Reload();

            FileSystemEntryViewModel? newEntry = FileEntries.FirstOrDefault(e =>
                string.Equals(e.Name, newName, StringComparison.OrdinalIgnoreCase)
            );
            if (newEntry is not null)
                SelectedFiles.Add(newEntry);
        }
        ForwardIfError(result);
    }

    private void RenameMultiple(string namingPattern)
    {
        IFileSystemEntry[] entries = SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry);
        if (!FileNameGenerator.CanBeRenamedCollectively(entries))
        {
            ForwardError("Selected entries aren't of the same type.");
            return;
        }

        RenameMultiple operation =
            new(
                _fileIOHandler,
                entries,
                FileNameGenerator.GetAvailableNames(entries, namingPattern)
            );
        IResult result = operation.Invoke();
        if (result.IsOk)
            _undoRedoHistory.AddNewNode(operation);

        ForwardIfError(result);
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
        MoveFilesToTrash operation =
            new(_fileIOHandler, SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry));

        IResult result = operation.Invoke();
        if (result.IsOk)
            _undoRedoHistory.AddNewNode(operation);

        ForwardIfError(result);
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
        IResult result = action.Invoke();
        if (result.IsOk)
            _undoRedoHistory.AddNewNode(action);

        ForwardIfError(result);
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
            ForwardIfError(
                entry.IsDirectory
                    ? _fileIOHandler.DeleteDir(entry.PathToEntry)
                    : _fileIOHandler.DeleteFile(entry.PathToEntry)
            );

        Reload();
    }

    /// <summary>
    /// Sets <see cref="SortBy"/> to the parameter and determines <see cref="SortReversed"/>.
    /// <para>
    /// Invokes <see cref="Reload"/>.
    /// </para>
    /// </summary>
    private void SetSortBy(SortBy sortBy)
    {
        SortReversed = SortBy == sortBy && !SortReversed;
        SortBy = sortBy;
        Reload();
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
                ForwardIfError(result);

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
                ForwardIfError(result);

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
            ForwardIfError(_versionControl.StagePath(entry.PathToEntry));
    }

    /// <summary>
    /// Relays the operations to <see cref="_versionControl"/>.
    /// </summary>
    public void UnstageFile(FileSystemEntryViewModel entry)
    {
        if (IsVersionControlled)
            ForwardIfError(_versionControl.UnstagePath(entry.PathToEntry));
    }

    /// <summary>
    /// Relays the operations to <see cref="_versionControl"/>.
    /// </summary>
    private void Pull()
    {
        if (IsVersionControlled)
        {
            ForwardIfError(_versionControl.DownloadChanges());
            Reload();
        }
    }

    /// <summary>
    /// Relays the operations to <see cref="_versionControl"/>.
    /// </summary>
    public void Commit(string commitMessage)
    {
        if (IsVersionControlled)
        {
            ForwardIfError(_versionControl.CommitChanges(commitMessage));
            Reload();
        }
    }

    /// <summary>
    /// Relays the operations to <see cref="_versionControl"/>.
    /// </summary>
    private void Push()
    {
        if (IsVersionControlled)
            ForwardIfError(_versionControl.UploadChanges());
    }

    /// <summary>
    /// Disposes <see cref="MainWindowViewModel"/> resources.
    /// </summary>
    public void Dispose()
    {
        _versionControl.Dispose();
        _searchCTS.Dispose();
        _refreshTimer?.Stop();
    }
}
#pragma warning restore CA1822 // Mark members as static
