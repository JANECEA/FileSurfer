using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.FileOperations;
using FileSurfer.Core.Models.FileOperations.Undoable;
using FileSurfer.Core.Models.Sftp;
using FileSurfer.Core.Models.Shell;
using FileSurfer.Core.Models.VersionControl;
using FileSurfer.Core.Views;
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
[ // Properties are used by the window, cannot be static have to global
    SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global"),
    SuppressMessage("ReSharper", "MemberCanBePrivate.Global"),
    SuppressMessage("ReSharper", "UnusedMember.Global"),
    SuppressMessage("Performance", "CA1822:Mark members as static"),
]
public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private string ThisPcLabel => FileSurferSettings.ThisPcLabel;

    private readonly IFileIoHandler _fileIoHandler;
    private readonly IBinInteraction _fileRestorer;
    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly IFileProperties _fileProperties;
    private readonly IShellHandler _shellHandler;
    private readonly IVersionControl _versionControl;
    private readonly IClipboardManager _clipboardManager;
    private readonly FileSystemEntryVmFactory _entryVmFactory;
    private readonly SearchManager _searchManager;
    private readonly UndoRedoHandler<IUndoableFileOperation> _undoRedoHistory;
    private readonly UndoRedoHandler<string> _pathHistory;
    private readonly Action<bool> _setDarkMode;
    private DispatcherTimer? _refreshTimer;

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
    private DateTime _lastModified;

    public SortInfo SortInfo => new(SortBy, SortReversed);

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
    public ObservableCollection<FileSystemEntryViewModel> SpecialFolders { get; } = [];

    /// <summary>
    /// Holds the Drives <see cref="FileSystemEntryViewModel"/>s.
    /// </summary>
    public FileSystemEntryViewModel[] Drives =>
        _drives ??= _fileInfoProvider.GetDrives().Select(_entryVmFactory.Drive).ToArray();
    private FileSystemEntryViewModel[]? _drives;

    /// <summary>
    /// TODO
    /// </summary>
    public ObservableCollection<SftpConnectionViewModel> SftpConnectionsVms { get; } = [];

    /// <summary>
    /// Holds the path to the current directory displayed in FileSurfer.
    /// </summary>
    public string CurrentDir
    {
        get => _currentDir;
        set => this.RaiseAndSetIfChanged(ref _currentDir, value);
    }
    private string _currentDir = string.Empty;

    /// <summary>
    /// Indicates whether the <see cref="CurrentInfoMessage"/> should be shown.
    /// </summary>
    public bool ShowInfoMessage
    {
        get => _showInfoMessage;
        set => this.RaiseAndSetIfChanged(ref _showInfoMessage, value);
    }
    private bool _showInfoMessage;

    /// <summary>
    /// Indicates whether the current directory contains files or directories.
    /// </summary>
    public string CurrentInfoMessage
    {
        get => _currentInfoMessage;
        set => this.RaiseAndSetIfChanged(ref _currentInfoMessage, value);
    }
    private string _currentInfoMessage = string.Empty;

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

    /// <summary>
    /// Initializes a new <see cref="MainWindowViewModel"/>.
    /// </summary>
    public MainWindowViewModel(
        string initialDir,
        IFileIoHandler fileIoHandler,
        IBinInteraction fileRestorer,
        IFileProperties fileProperties,
        IFileInfoProvider fileInfoProvider,
        IIconProvider iconProvider,
        IShellHandler shellHandler,
        IVersionControl versionControl,
        IClipboardManager clipboardManager,
        Action<bool> setDarkMode
    )
    {
        _fileIoHandler = fileIoHandler;
        _fileRestorer = fileRestorer;
        _fileProperties = fileProperties;
        _fileInfoProvider = fileInfoProvider;
        _shellHandler = shellHandler;
        _versionControl = versionControl;
        _clipboardManager = clipboardManager;
        _setDarkMode = setDarkMode;
        _entryVmFactory = new FileSystemEntryVmFactory(
            _fileInfoProvider,
            _fileProperties,
            iconProvider
        );
        _searchManager = new SearchManager(
            _entryVmFactory,
            _fileInfoProvider,
            s => CurrentDir = s,
            entry => FileEntries.Add(entry)
        );
        _undoRedoHistory = new UndoRedoHandler<IUndoableFileOperation>();
        _pathHistory = new UndoRedoHandler<string>();

        GoBackCommand = ReactiveCommand.Create(GoBack);
        GoForwardCommand = ReactiveCommand.Create(GoForward);
        GoUpCommand = ReactiveCommand.Create(GoUp);
        ReloadCommand = ReactiveCommand.Create(() => Reload(true));
        OpenPowerShellCommand = ReactiveCommand.Create(OpenPowerShell);
        CancelSearchCommand = ReactiveCommand.Create(() => CancelSearch(null));
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
        LoadSettings(false);
        SetCurrentDir(initialDir);
        FileSurferSettings.OnSettingsChange = () => LoadSettings(true);
        SelectedFiles.CollectionChanged += UpdateSelectionInfo;
        FileEntries.CollectionChanged += UpdateSelectionInfo;
    }

    private void LoadSettings(bool reload)
    {
        if (!FileSurferSettings.ShowSpecialFolders)
            SpecialFolders.Clear();
        else if (SpecialFolders.Count == 0)
            foreach (FileSystemEntryViewModel entry in GetSpecialFolders())
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
            _lastModified = DateTime.Now;
            _refreshTimer.Tick += CheckForUpdates;
            _refreshTimer.Start();
        }

        _setDarkMode(FileSurferSettings.UseDarkMode);
        if (reload)
            Reload(true);
    }

    private void SetCurrentDirNoHistory(string dirPath)
    {
        if (Directory.Exists(dirPath))
        {
            dirPath = Path.GetFullPath(dirPath);
            _lastModified = Directory.GetLastWriteTime(dirPath);
        }
        else if (dirPath != ThisPcLabel && !Searching)
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

            SetSearchWaterMark(dirPath);
            Reload(true);
        }
    }

    /// <summary>
    /// Sets the <see cref="CurrentDir"/> and adds the directory to <see cref="_pathHistory"/>,
    /// </summary>
    public void SetCurrentDir(string dirPath)
    {
        SetCurrentDirNoHistory(dirPath);

        if (IsValidDirectory(dirPath) && CurrentDir != _pathHistory.Current)
            _pathHistory.AddNewNode(dirPath);
    }

    public void ShowMessage(string message)
    {
        ShowInfoMessage = true;
        CurrentInfoMessage = message;
    }

    private void HideMessage() => ShowInfoMessage = false;

    /// <summary>
    /// Compares <see cref="_lastModified"/> to the latest <see cref="Directory.GetLastWriteTime(string)"/>.
    /// <para>
    /// Invokes <see cref="Reload"/> if <see cref="Directory.GetLastWriteTime(string)"/> is newer.
    /// </para>
    /// </summary>
    private void CheckForUpdates(object? sender, EventArgs e)
    {
        if (Directory.Exists(CurrentDir))
        {
            if (CompareSetLastWriteTime())
                Reload(true);
            else if (IsVersionControlled)
                Reload(false);
        }
    }

    private bool CompareSetLastWriteTime()
    {
        DateTime lastWriteTime = Directory.GetLastWriteTime(CurrentDir);
        if (lastWriteTime <= _lastModified)
            return false;

        _lastModified = lastWriteTime;
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

        CheckVersionControl();

        if (CurrentDir == ThisPcLabel)
            ShowDrives();
        else if (Directory.Exists(CurrentDir))
            LoadEntries(forceHardReload);
        else
        {
            ForwardError($"Directory: \"{CurrentDir}\" does not exist.");
            SetCurrentDir(GetClosestExistingParent(CurrentDir));
            return;
        }

        if (FileEntries.Count == 0)
            ShowMessage("This directory is empty");
        else
            HideMessage();

        _lastModified = DateTime.Now;
    }

    private string GetClosestExistingParent(string dirPath)
    {
        string? path = dirPath;

        while (!Directory.Exists(path))
            if ((path = Path.GetDirectoryName(path)) is null)
                return ThisPcLabel;

        return path;
    }

    /// <summary>
    /// Opens a new <see cref="ErrorWindow"/> dialog.
    /// </summary>
    private void ForwardError(string? errorMessage)
    {
        if (!string.IsNullOrEmpty(errorMessage))
            Dispatcher.UIThread.Post(() =>
            {
                new ErrorWindow { ErrorMessage = errorMessage }.Show();
            });
    }

    /// <summary>
    /// Opens a new <see cref="ErrorWindow"/> dialog if the result is failed.
    /// </summary>
    private void ForwardIfError(IResult result)
    {
        if (!result.IsOk)
            foreach (string errorMessage in result.Errors)
                ForwardError(errorMessage);
    }

    /// <summary>
    /// Used for setting the text selection when renaming files.
    /// </summary>
    /// <returns>The length of the file name without extension.</returns>
    public int GetNameEndIndex(FileSystemEntryViewModel entry) =>
        SelectedFiles.Count > 0 ? entry.FileSystemEntry.NameWoExtension.Length : 0;

    private bool IsValidDirectory(string path) =>
        path == ThisPcLabel || (!string.IsNullOrEmpty(path) && Directory.Exists(path));

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
        if (CurrentDir == ThisPcLabel)
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
            ForwardIfError(_fileProperties.ShowOpenAsDialog(entry.FileSystemEntry));
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
                ForwardIfError(_shellHandler.OpenInNotepad(entry.PathToEntry));
    }

    /// <summary>
    /// Opens the location of the selected entry during searching.
    /// </summary>
    public void OpenEntryLocation(FileSystemEntryViewModel entry) =>
        SetCurrentDir(Path.GetDirectoryName(entry.PathToEntry) ?? ThisPcLabel);

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
        if (Path.GetDirectoryName(PathTools.NormalizePath(CurrentDir)) is not string parentDir)
            SetCurrentDir(ThisPcLabel);
        else if (CurrentDir != ThisPcLabel)
            SetCurrentDir(parentDir);
    }

    private IEnumerable<FileSystemEntryViewModel> GetSpecialFolders() =>
        _fileInfoProvider
            .GetSpecialFolders()
            .Where(dirPath => !string.IsNullOrEmpty(dirPath))
            .Select(_entryVmFactory.Directory);

    private void LoadQuickAccess()
    {
        foreach (string path in FileSurferSettings.QuickAccess)
            if (Directory.Exists(path))
                QuickAccess.Add(_entryVmFactory.Directory(path));
            else if (File.Exists(path))
                QuickAccess.Add(_entryVmFactory.File(path));
    }

    private void ShowDrives()
    {
        FileEntries.Clear();
        foreach (FileSystemEntryViewModel drive in Drives)
            FileEntries.Add(drive);
    }

    private void LoadEntries(bool forceHardReload)
    {
        if (forceHardReload || CompareSetLastWriteTime())
            LoadDirEntries();
        else if (IsVersionControlled)
            foreach (FileSystemEntryViewModel entry in FileEntries)
                entry.UpdateVcStatus(GetVcStatus(entry.PathToEntry));
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

        FileSystemEntryViewModel[] dirs = dirPaths.ConvertToArray(path =>
            _entryVmFactory.Directory(path, GetVcStatus(path))
        );
        FileSystemEntryViewModel[] files = filePaths.ConvertToArray(path =>
            _entryVmFactory.File(path, GetVcStatus(path))
        );

        AddEntries(dirs, files);
    }

    private VcStatus GetVcStatus(string path) =>
        IsVersionControlled ? _versionControl.GetStatus(path) : VcStatus.NotVersionControlled;

    /// <summary>
    /// Adds directories and files to <see cref="FileEntries"/>.
    /// </summary>
    private void AddEntries(FileSystemEntryViewModel[] dirs, FileSystemEntryViewModel[] files)
    {
        SortInPlace(files, SortBy);
        SortInPlace(dirs, SortBy);

        HashSet<string>? selectedPaths = null;
        if (SelectedFiles.Count > 0)
            selectedPaths = SelectedFiles.Select(entry => entry.PathToEntry).ToHashSet();

        FileEntries.Clear();
        if (!SortReversed || SortBy is SortBy.Type or SortBy.Size)
            for (int i = 0; i < dirs.Length; i++)
                FileEntries.Add(dirs[i]);
        else
            for (int i = dirs.Length - 1; i >= 0; i--)
                FileEntries.Add(dirs[i]);

        if (!SortReversed)
            for (int i = 0; i < files.Length; i++)
                FileEntries.Add(files[i]);
        else
            for (int i = files.Length - 1; i >= 0; i--)
                FileEntries.Add(files[i]);

        if (selectedPaths is null)
            return;

        foreach (FileSystemEntryViewModel entry in FileEntries)
            if (selectedPaths.Contains(entry.PathToEntry))
                SelectedFiles.Add(entry);
    }

    private void SortInPlace(FileSystemEntryViewModel[] entries, SortBy sortBy)
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
    private void CheckVersionControl()
    {
        IsVersionControlled =
            FileSurferSettings.GitIntegration
            && Directory.Exists(CurrentDir)
            && _versionControl.InitIfVersionControlled(CurrentDir);

        LoadBranches();
    }

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
            SetCurrentDirNoHistory(nextPath);
        else
            _pathHistory.RemoveNode(true);
    }

    /// <summary>
    /// Opens PowerShell in <see cref="CurrentDir"/> if possible.
    /// </summary>
    private void OpenPowerShell()
    {
        if (CurrentDir != ThisPcLabel && !Searching)
            ForwardIfError(_shellHandler.OpenCmdAt(CurrentDir));
    }

    public async Task SearchAsync(string searchQuery)
    {
        if (Searching)
        {
            HideMessage();
            await _searchManager.CancelSearchAsync();
        }

        string currentDir = _pathHistory.Current ?? ThisPcLabel;
        Searching = true;
        FileEntries.Clear();

        int? foundEntries =
            currentDir != ThisPcLabel
                ? await _searchManager.SearchAsync(searchQuery, [currentDir])
                : await _searchManager.SearchAsync(
                    searchQuery,
                    _fileInfoProvider.GetDrives().Select(drive => drive.PathToEntry)
                );

        if (foundEntries is 0)
            ShowMessage("No items match your query");
    }

    /// <summary>
    /// Cancels the search and sets <see cref="CurrentDir"/> to the parameter or last directory.
    /// </summary>
    public void CancelSearch(string? directory = null)
    {
        Searching = false;
        _searchManager.CancelSearch();
        SetCurrentDir(directory ?? _pathHistory.Current ?? ThisPcLabel);
    }

    /// <summary>
    /// Creates a new file using <see cref="_fileIoHandler"/> in <see cref="CurrentDir"/>
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

        NewFileAt operation = new(_fileIoHandler, CurrentDir, newFileName);
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
    /// Creates a new directory using <see cref="_fileIoHandler"/> in <see cref="CurrentDir"/>
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

        NewDirAt operation = new(_fileIoHandler, CurrentDir, newDirName);
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
            SelectedFiles[^1].FileSystemEntry.NameWoExtension + ArchiveManager.ArchiveTypeExtension;

        ForwardIfError(
            await Task.Run(() =>
                ArchiveManager.ZipFiles(
                    SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry),
                    CurrentDir,
                    FileNameGenerator.GetAvailableName(CurrentDir, archiveName)
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
        if (!Directory.Exists(CurrentDir))
            return;

        foreach (FileSystemEntryViewModel entry in SelectedFiles)
            if (!ArchiveManager.IsZipped(entry.PathToEntry))
            {
                ForwardError($"Entry \"{entry.Name}\" is not an archive.");
                return;
            }

        await Task.Run(() =>
        {
            foreach (string path in SelectedFiles.Select(entry => entry.PathToEntry).ToArray())
                ForwardIfError(ArchiveManager.UnzipArchive(path, CurrentDir));
        });
        Reload();
    }

    /// <summary>
    /// Copies the path to the selected <see cref="FileSystemEntryViewModel"/> to the system clipboard.
    /// </summary>
    public void CopyPath(FileSystemEntryViewModel entry) =>
        _clipboardManager.CopyPathToFileAsync(entry.PathToEntry);

    /// <summary>
    /// Relays the current selection in <see cref="SelectedFiles"/> to <see cref="_clipboardManager"/>.
    /// </summary>
    public async Task Cut()
    {
        if (SelectedFiles.Count > 0)
            ForwardIfError(
                await _clipboardManager.CutAsync(
                    SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry),
                    CurrentDir
                )
            );
    }

    /// <summary>
    /// Relays the current selection in <see cref="SelectedFiles"/> to <see cref="_clipboardManager"/>.
    /// </summary>
    public async Task Copy()
    {
        if (SelectedFiles.Count > 0)
            ForwardIfError(
                await _clipboardManager.CopyAsync(
                    SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry),
                    CurrentDir
                )
            );
    }

    /// <summary>
    /// Determines the type of paste operation and executes it using <see cref="_clipboardManager"/>.
    /// <para>
    /// Adds the appropriate <see cref="IUndoableFileOperation"/> to <see cref="_pathHistory"/> if the operation was a success.
    /// </para>
    /// Invokes <see cref="Reload"/>.
    /// </summary>
    private async Task Paste()
    {
        if (
            FileSurferSettings.AllowImagePastingFromClipboard
            && (await _clipboardManager.PasteImageAsync(CurrentDir)).IsOk
        )
            return;

        PasteType pasteType = await _clipboardManager.GetOperationType(CurrentDir);

        ValueResult<IUndoableFileOperation> result =
            pasteType == PasteType.Duplicate ? await DuplicateFiles() : await PasteFiles(pasteType);

        if (result.IsOk)
            _undoRedoHistory.AddNewNode(result.Value);

        ForwardIfError(result);
        Reload();
    }

    private async Task<ValueResult<IUndoableFileOperation>> PasteFiles(PasteType pasteType)
    {
        ValueResult<IFileSystemEntry[]> pastedResult = await _clipboardManager.PasteAsync(
            CurrentDir,
            pasteType
        );
        if (!pastedResult.IsOk)
            return ValueResult<IUndoableFileOperation>.Error(pastedResult);

        return ValueResult<IUndoableFileOperation>.Ok(
            pasteType is PasteType.Cut
                ? new MoveFilesTo(_fileIoHandler, pastedResult.Value, CurrentDir)
                : new CopyFilesTo(_fileIoHandler, pastedResult.Value, CurrentDir)
        );
    }

    private async Task<ValueResult<IUndoableFileOperation>> DuplicateFiles()
    {
        IFileSystemEntry[] clipboard = _clipboardManager.GetClipboard();

        ValueResult<string[]> copyNamesResult = await _clipboardManager.Duplicate(CurrentDir);
        if (!copyNamesResult.IsOk)
            return ValueResult<IUndoableFileOperation>.Error(copyNamesResult);

        return ValueResult<IUndoableFileOperation>.Ok(
            new DuplicateFiles(_fileIoHandler, clipboard, copyNamesResult.Value)
        );
    }

    /// <summary>
    /// Relays the operation to <see cref="_fileIoHandler"/> and invokes <see cref="Reload"/>.
    /// </summary>
    public void CreateShortcut(FileSystemEntryViewModel entry)
    {
        IResult result = entry.IsDirectory
            ? _shellHandler.CreateDirectoryLink(entry.PathToEntry)
            : _shellHandler.CreateFileLink(entry.PathToEntry);

        ForwardIfError(result);
        Reload();
    }

    /// <summary>
    /// Relays the operation to <see cref="_fileIoHandler"/>.
    /// </summary>
    public void ShowProperties(FileSystemEntryViewModel entry) =>
        ForwardIfError(_fileProperties.ShowFileProperties(entry));

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

        RenameOne operation = new(_fileIoHandler, entry.FileSystemEntry, newName);
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

        RenameMultiple operation = new(
            _fileIoHandler,
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
    /// Moves the <see cref="FileSystemEntryViewModel"/>s in <see cref="SelectedFiles"/> to the system trash using <see cref="_fileIoHandler"/>.
    /// <para>
    /// Adds the operation to <see cref="_undoRedoHistory"/> if the operation was successful.
    /// </para>
    /// Invokes <see cref="Reload"/>.
    /// </summary>
    public void MoveToTrash()
    {
        MoveFilesToTrash operation = new(
            _fileRestorer,
            _fileIoHandler,
            SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry)
        );

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

        FlattenFolder action = new(_fileIoHandler, _fileInfoProvider, entry.PathToEntry);
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
                    ? _fileIoHandler.DeleteDir(entry.PathToEntry)
                    : _fileIoHandler.DeleteFile(entry.PathToEntry)
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
    /// Call on app closing
    /// </summary>
    public void CloseApp()
    {
        FileSurferSettings.UpdateQuickAccess(QuickAccess);
    }

    public void Dispose()
    {
        _versionControl.Dispose();
        _refreshTimer?.Stop();
        _entryVmFactory.Dispose();

        if (Searching)
            CancelSearch();
        _searchManager.Dispose();
    }
}
