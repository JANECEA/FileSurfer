using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FileSurfer.Models;
using FileSurfer.Models.UndoableFileOperations;
using ReactiveUI;

namespace FileSurfer.ViewModels;

#pragma warning disable CA1822 // Mark members as static
public class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
{
    private const string SearchingLabel = "Search Results";
    private const long ShowDialogLimitB = 262144000; // 250 MiB
    private const int ArraySearchThreshold = 25;
    private readonly string ThisPCLabel = FileSurferSettings.ThisPCLabel;
    private readonly string NewFileName = FileSurferSettings.NewFileName;
    private readonly string NewDirName = FileSurferSettings.NewDirectoryName;
    private readonly string NewImageName = FileSurferSettings.NewImageName + ".png";

    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly IVersionControl _versionControl;
    private readonly UndoRedoHandler<IUndoableFileOperation> _undoRedoHistory;
    private readonly UndoRedoHandler<string> _pathHistory;
    private readonly ClipboardManager _clipboardManager;
    private readonly DispatcherTimer? _refreshTimer;

    private CancellationTokenSource _searchCTS = new();
    private bool _isUserInvoked = true;
    private DateTime _lastModified;

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

    private readonly ObservableCollection<FileSystemEntry> _fileEntries = new();
    private readonly ObservableCollection<FileSystemEntry> _selectedFiles = new();
    private readonly ObservableCollection<FileSystemEntry> _quickAccess = new();
    public ObservableCollection<FileSystemEntry> FileEntries => _fileEntries;
    public ObservableCollection<FileSystemEntry> SelectedFiles => _selectedFiles;
    public ObservableCollection<FileSystemEntry> QuickAccess => _quickAccess;
    public FileSystemEntry[] SpecialFolders { get; }
    public FileSystemEntry[] Drives { get; }

    private string? _errorMessage = null;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            this.RaiseAndSetIfChanged(ref _errorMessage, value);
            if (!string.IsNullOrEmpty(value))
            {
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await ShowErrorWindowAsync(value);
                });
            }
        }
    }

    private async Task ShowErrorWindowAsync(string errorMessage) =>
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            new Views.ErrorWindow(errorMessage).Show();
        });

    private string _currentDir = string.Empty;
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

                if (_isUserInvoked && value != _pathHistory.Current)
                    _pathHistory.AddNewNode(value);
            }
            if (Directory.Exists(value))
                _lastModified = Directory.GetLastWriteTime(value);
        }
    }

    private bool _directoryEmpty;
    public bool DirectoryEmpty
    {
        get => _directoryEmpty;
        set => this.RaiseAndSetIfChanged(ref _directoryEmpty, value);
    }

    private string _searchWaterMark = string.Empty;
    public string SearchWaterMark
    {
        get => _searchWaterMark;
        set => this.RaiseAndSetIfChanged(ref _searchWaterMark, value);
    }

    private string _selectionInfo = string.Empty;
    public string SelectionInfo
    {
        get => _selectionInfo;
        set => this.RaiseAndSetIfChanged(ref _selectionInfo, value);
    }

    private bool _searching;
    public bool Searching
    {
        get => _searching;
        set => this.RaiseAndSetIfChanged(ref _searching, value);
    }

    private readonly ObservableCollection<string> _branches = new();
    public ObservableCollection<string> Branches => _branches;

    private string? _currentBranch;
    public string? CurrentBranch
    {
        get => _currentBranch;
        set
        {
            if (!string.IsNullOrEmpty(value) && Branches.Contains(value))
            {
                _versionControl.SwitchBranches(value, out string? errorMessage);
                ErrorMessage = errorMessage;
            }
            _currentBranch = value;
        }
    }

    private int _currentBranchIndex = -1;
    public int CurrentBranchIndex
    {
        get => _currentBranchIndex;
        set => this.RaiseAndSetIfChanged(ref _currentBranchIndex, value);
    }

    private bool _isVersionControlled = false;
    public bool IsVersionControlled
    {
        get => _isVersionControlled;
        set => this.RaiseAndSetIfChanged(ref _isVersionControlled, value);
    }

    public ReactiveCommand<Unit, Unit> GoBackCommand { get; }
    public ReactiveCommand<Unit, Unit> GoForwardCommand { get; }
    public ReactiveCommand<Unit, Unit> ReloadCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenPowerShellCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelSearchCommand { get; }
    public ReactiveCommand<Unit, Unit> NewFileCommand { get; }
    public ReactiveCommand<Unit, Unit> NewDirCommand { get; }
    public ReactiveCommand<Unit, Unit> CutCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyCommand { get; }
    public ReactiveCommand<Unit, Unit> PasteCommand { get; }
    public ReactiveCommand<Unit, Unit> MoveToTrashCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> SortByNameCommand { get; }
    public ReactiveCommand<Unit, Unit> SortByDateCommand { get; }
    public ReactiveCommand<Unit, Unit> SortByTypeCommand { get; }
    public ReactiveCommand<Unit, Unit> SortBySizeCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectNoneCommand { get; }
    public ReactiveCommand<Unit, Unit> InvertSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> PullCommand { get; }
    public ReactiveCommand<Unit, Unit> PushCommand { get; }

    public MainWindowViewModel()
    {
        _fileOpsHandler = new WindowsFileOperationsHandler(ShowDialogLimitB);
        _versionControl = new GitVersionControlHandler(_fileOpsHandler);
        _clipboardManager = new ClipboardManager(_fileOpsHandler, NewImageName);
        _undoRedoHistory = new();
        _pathHistory = new();
        _selectedFiles.CollectionChanged += UpdateSelectionInfo;
        GoBackCommand = ReactiveCommand.Create(GoBack);
        GoForwardCommand = ReactiveCommand.Create(GoForward);
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
        OpenSettingsCommand = ReactiveCommand.Create(OpenSettings);
        PullCommand = ReactiveCommand.Create(Pull);
        PushCommand = ReactiveCommand.Create(Push);

        LoadQuickAccess();
        Drives = GetDrives();
        SpecialFolders = FileSurferSettings.ShowSpecialFolders
            ? GetSpecialFolders()
            : Array.Empty<FileSystemEntry>();

        CurrentDir = IsValidDirectory(FileSurferSettings.OpenIn)
            ? FileSurferSettings.OpenIn
            : ThisPCLabel;
        _pathHistory.AddNewNode(CurrentDir);

        if (FileSurferSettings.AutomaticRefresh)
        {
            _refreshTimer = new()
            {
                Interval = TimeSpan.FromMilliseconds(FileSurferSettings.AutomaticRefreshInterval),
            };
            _lastModified = Directory.GetLastWriteTime(CurrentDir);
            _refreshTimer.Tick += CheckForUpdates;
            _refreshTimer.Start();
        }
    }

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

    private void Reload()
    {
        if (Searching)
            return;

        CheckVersionContol();
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
    }

    private void OpenSettings()
    {
        _fileOpsHandler.OpenFile(FileSurferSettings.SettingsFilePath, out string? errorMessage);
        ErrorMessage = errorMessage;
    }

    public int GetNameEndIndex(FileSystemEntry entry) =>
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

    private void UpdateSelectionInfo(
        object? sender = null,
        NotifyCollectionChangedEventArgs? e = null
    )
    {
        if (CurrentDir == ThisPCLabel)
            return;

        string selectionInfo = FileEntries.Count == 1 ? "1 item" : $"{FileEntries.Count} items";

        if (SelectedFiles.Count == 1)
            selectionInfo += $"  |  1 item selected";
        else if (SelectedFiles.Count > 1)
            selectionInfo += $"  |  {SelectedFiles.Count} items selected";

        bool displaySize = SelectedFiles.Count >= 1;
        long sizeSum = 0;
        foreach (FileSystemEntry entry in SelectedFiles)
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
            selectionInfo += "  " + FileSystemEntry.GetSizeString(sizeSum);

        SelectionInfo = selectionInfo;
    }

    public void OpenEntry(FileSystemEntry entry)
    {
        if (entry.IsDirectory)
            CurrentDir = entry.PathToEntry;
        else if (_fileOpsHandler.IsLinkedToDirectory(entry.PathToEntry, out string? directory))
        {
            if (directory is not null)
                CurrentDir = directory;
        }
        else
        {
            _fileOpsHandler.OpenFile(entry.PathToEntry, out string? errorMessage);
            ErrorMessage = errorMessage;
        }
    }

    public void OpenAs(FileSystemEntry entry)
    {
        if (!entry.IsDirectory)
        {
            WindowsFileProperties.ShowOpenAsDialog(entry.PathToEntry, out string? errorMessage);
            ErrorMessage = errorMessage;
        }
    }

    public void OpenEntries()
    {
        if (SelectedFiles.Count > 1)
        {
            foreach (FileSystemEntry entry in SelectedFiles)
                if (
                    entry.IsDirectory
                    || _fileOpsHandler.IsLinkedToDirectory(entry.PathToEntry, out _)
                )
                    return;
        }
        // To prevent collection changing during iteration
        foreach (FileSystemEntry entry in SelectedFiles.ToArray())
            OpenEntry(entry);
    }

    public void OpenInNotepad()
    {
        foreach (FileSystemEntry entry in SelectedFiles)
        {
            if (!entry.IsDirectory)
            {
                _fileOpsHandler.OpenInNotepad(entry.PathToEntry, out string? errorMessage);
                ErrorMessage = errorMessage;
            }
        }
    }

    public void AddToQuickAccess(FileSystemEntry entry) => QuickAccess.Add(entry);

    public void RemoveFromQuickAccess(FileSystemEntry entry) => QuickAccess.Remove(entry);

    public void OpenEntryLocation(FileSystemEntry entry) =>
        CurrentDir = Path.GetDirectoryName(entry.PathToEntry) ?? ThisPCLabel;

    public void GoUp()
    {
        if (Path.GetDirectoryName(CurrentDir) is not string parentDir)
            CurrentDir = ThisPCLabel;
        else if (CurrentDir != ThisPCLabel)
            CurrentDir = parentDir;
    }

    private FileSystemEntry[] GetDrives() =>
        _fileOpsHandler.GetDrives().Select(driveInfo => new FileSystemEntry(driveInfo)).ToArray();

    private FileSystemEntry[] GetSpecialFolders() =>
        _fileOpsHandler
            .GetSpecialFolders()
            .Where(dirPath => !string.IsNullOrEmpty(dirPath))
            .Select(dirPath => new FileSystemEntry(_fileOpsHandler, dirPath, true))
            .ToArray();

    private void LoadQuickAccess()
    {
        foreach (string path in FileSurferSettings.QuickAccess)
        {
            if (Directory.Exists(path))
                QuickAccess.Add(new FileSystemEntry(_fileOpsHandler, path, true));
            else if (File.Exists(path))
                QuickAccess.Add(new FileSystemEntry(_fileOpsHandler, path, false));
        }
    }

    private void ShowDrives()
    {
        FileEntries.Clear();
        foreach (FileSystemEntry drive in Drives)
            FileEntries.Add(drive);
    }

    private void LoadDirEntries()
    {
        string[] dirPaths = _fileOpsHandler.GetPathDirs(
            CurrentDir,
            FileSurferSettings.ShowHiddenFiles,
            FileSurferSettings.ShowProtectedFiles
        );
        string[] filePaths = _fileOpsHandler.GetPathFiles(
            CurrentDir,
            FileSurferSettings.ShowHiddenFiles,
            FileSurferSettings.ShowProtectedFiles
        );
        if (FileSurferSettings.TreatDotFilesAsHidden && !FileSurferSettings.ShowHiddenFiles)
            RemoveDotFiles(ref dirPaths, ref filePaths);

        FileSystemEntry[] directories = new FileSystemEntry[dirPaths.Length];
        FileSystemEntry[] files = new FileSystemEntry[filePaths.Length];

        for (int i = 0; i < dirPaths.Length; i++)
            directories[i] = new FileSystemEntry(_fileOpsHandler, dirPaths[i], true);

        for (int i = 0; i < filePaths.Length; i++)
            files[i] = new FileSystemEntry(
                _fileOpsHandler,
                filePaths[i],
                false,
                GetVCState(filePaths[i])
            );

        SortAndAddEntries(directories, files);
    }

    private void RemoveDotFiles(ref string[] dirPaths, ref string[] filePaths)
    {
        dirPaths = dirPaths.Where(path => !Path.GetFileName(path).StartsWith('.')).ToArray();
        filePaths = filePaths.Where(path => !Path.GetFileName(path).StartsWith('.')).ToArray();
    }

    private VCStatus GetVCState(string path)
    {
        if (!IsVersionControlled)
            return VCStatus.NotVersionControlled;

        return _versionControl.ConsolidateStatus(path);
    }

    private void SortAndAddEntries(FileSystemEntry[] directories, FileSystemEntry[] files)
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

    private void SortInPlace(FileSystemEntry[] entries, SortBy sortBy)
    {
        switch (sortBy)
        {
            case SortBy.Name:
                Array.Sort(entries, (x, y) => string.Compare(x.Name, y.Name));
                break;

            case SortBy.Date:
                Array.Sort(entries, (x, y) => DateTime.Compare(y.LastModTime, x.LastModTime));
                break;

            case SortBy.Type:
                Array.Sort(entries, (x, y) => string.Compare(x.Type, y.Type));
                break;

            case SortBy.Size:
                Array.Sort(entries, (x, y) => (y.SizeB ?? 0).CompareTo(x.SizeB ?? 0));
                break;

            default:
                throw new ArgumentException($"Unsupported sort option: {sortBy}", nameof(sortBy));
        }
    }

    private void CheckVersionContol()
    {
        IsVersionControlled =
            FileSurferSettings.GitIntegration
            && Directory.Exists(CurrentDir)
            && _versionControl.IsVersionControlled(CurrentDir);

        Branches.Clear();
        if (IsVersionControlled)
        {
            foreach (string branch in _versionControl.GetBranches())
                Branches.Add(branch);

            CurrentBranchIndex = Branches.IndexOf(_versionControl.GetCurrentBranchName());
        }
    }

    private void CheckDirectoryEmpty() => DirectoryEmpty = FileEntries.Count == 0;

    public void GoBack()
    {
        if (Searching)
            CancelSearch();

        if (_pathHistory.GetPrevious() is not string previousPath)
            return;

        _pathHistory.MoveToPrevious();

        if (IsValidDirectory(previousPath))
        {
            _isUserInvoked = false;
            CurrentDir = previousPath;
            _isUserInvoked = true;
        }
        else
            _pathHistory.RemoveNode(false);
    }

    public void GoForward()
    {
        if (Searching)
            CancelSearch();

        if (_pathHistory.GetNext() is not string nextPath)
            return;

        _pathHistory.MoveToNext();

        if (IsValidDirectory(nextPath))
        {
            _isUserInvoked = false;
            CurrentDir = nextPath;
            _isUserInvoked = true;
        }
        else
            _pathHistory.RemoveNode(true);
    }

    private void OpenPowerShell()
    {
        if (CurrentDir == ThisPCLabel || Searching)
            return;

        _fileOpsHandler.OpenCmdAt(CurrentDir, out string? errorMessage);
        ErrorMessage = errorMessage;
    }

    public async void SearchRelay(string searchQuerry)
    {
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
            await SearchDirectoryAsync(currentDir, searchQuerry, _searchCTS.Token);
        else
        {
            foreach (DriveInfo drive in _fileOpsHandler.GetDrives())
                await SearchDirectoryAsync(drive.Name, searchQuerry, _searchCTS.Token);
        }
    }

    private async Task SearchDirectoryAsync(
        string directory,
        string searchQuery,
        CancellationToken searchCTS
    )
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            foreach (string file in await GetPathFilesAsync(directory, searchQuery))
                if (!searchCTS.IsCancellationRequested)
                    FileEntries.Add(new FileSystemEntry(_fileOpsHandler, file, false));

            foreach (string dir in await GetPathDirsAsync(directory, searchQuery))
                if (!searchCTS.IsCancellationRequested)
                    FileEntries.Add(new FileSystemEntry(_fileOpsHandler, dir, true));
        });

        foreach (string dir in await GetPathDirsAsync(directory))
            if (!searchCTS.IsCancellationRequested)
                await SearchDirectoryAsync(dir, searchQuery, searchCTS);
    }

    private async Task<IEnumerable<string>> GetPathFilesAsync(string directory, string query)
    {
        IEnumerable<string> entries = await Task.Run(
            () =>
                _fileOpsHandler.GetPathFiles(
                    directory,
                    FileSurferSettings.ShowHiddenFiles,
                    FileSurferSettings.ShowProtectedFiles
                )
        );
        if (!FileSurferSettings.ShowHiddenFiles && FileSurferSettings.TreatDotFilesAsHidden)
            entries = entries.Where(path => Path.GetFileName(path).StartsWith('.'));

        return entries.Where(name =>
            Path.GetFileName(name).Contains(query, StringComparison.CurrentCultureIgnoreCase)
        );
    }

    private async Task<IEnumerable<string>> GetPathDirsAsync(string directory, string? query = null)
    {
        IEnumerable<string> entries = await Task.Run(
            () =>
                _fileOpsHandler.GetPathDirs(
                    directory,
                    FileSurferSettings.ShowHiddenFiles,
                    FileSurferSettings.ShowProtectedFiles
                )
        );
        if (!FileSurferSettings.ShowHiddenFiles && FileSurferSettings.TreatDotFilesAsHidden)
            entries = entries.Where(path => Path.GetFileName(path).StartsWith('.'));

        return query is null
            ? entries
            : entries.Where(name =>
                Path.GetFileName(name).Contains(query, StringComparison.CurrentCultureIgnoreCase)
            );
    }

    public void CancelSearch()
    {
        _searchCTS.Cancel();
        Searching = false;
        CurrentDir = _pathHistory.Current ?? ThisPCLabel;
    }

    public void CancelSearch(string directory)
    {
        _searchCTS.Cancel();
        Searching = false;
        CurrentDir = directory;
    }

    private void NewFile()
    {
        string newFileName = FileNameGenerator.GetAvailableName(CurrentDir, NewFileName);
        if (_fileOpsHandler.NewFileAt(CurrentDir, newFileName, out string? errorMessage))
        {
            Reload();
            _undoRedoHistory.AddNewNode(new NewFileAt(_fileOpsHandler, CurrentDir, newFileName));
            SelectedFiles.Add(FileEntries.First(entry => entry.Name == newFileName));
        }
        else
            ErrorMessage = errorMessage;
    }

    private void NewDir()
    {
        string newDirName = FileNameGenerator.GetAvailableName(CurrentDir, NewDirName);
        if (_fileOpsHandler.NewDirAt(CurrentDir, newDirName, out string? errorMessage))
        {
            Reload();
            _undoRedoHistory.AddNewNode(new NewDirAt(_fileOpsHandler, CurrentDir, newDirName));
            SelectedFiles.Add(FileEntries.First(entry => entry.Name == newDirName));
        }
        else
            ErrorMessage = errorMessage;
    }

    public void AddToArchive()
    {
        if (!Directory.Exists(CurrentDir))
            return;

        string fileName = Path.GetFileNameWithoutExtension(SelectedFiles[^1].Name) + ".zip";
        ArchiveManager.ZipFiles(
            SelectedFiles.Select(entry => entry.PathToEntry).ToArray(),
            CurrentDir,
            FileNameGenerator.GetAvailableName(CurrentDir, fileName),
            out string? errorMessage
        );
        Reload();
        ErrorMessage = errorMessage;
    }

    public void ExtractArchive()
    {
        if (!Directory.Exists(CurrentDir))
            return;

        foreach (FileSystemEntry entry in SelectedFiles)
            if (!ArchiveManager.IsZipped(entry.PathToEntry))
                return;

        foreach (FileSystemEntry entry in SelectedFiles)
        {
            ArchiveManager.UnzipArchive(entry.PathToEntry, CurrentDir, out string? errorMessage);
            ErrorMessage = errorMessage;
        }
        Reload();
    }

    public void CopyPath(FileSystemEntry entry) =>
        _clipboardManager.CopyPathToFile(entry.PathToEntry);

    public void Cut()
    {
        _clipboardManager.Cut(SelectedFiles.ToList(), CurrentDir, out string? errorMessage);
        ErrorMessage = errorMessage;
    }

    public void Copy()
    {
        _clipboardManager.Copy(SelectedFiles.ToList(), CurrentDir, out string? errorMessage);
        ErrorMessage = errorMessage;
    }

    private void Paste()
    {
        string? errorMessage;
        if (_clipboardManager.IsDuplicateOperation(CurrentDir))
        {
            if (_clipboardManager.Duplicate(CurrentDir, out string[] copyNames, out errorMessage))
                _undoRedoHistory.AddNewNode(
                    new DuplicateFiles(_fileOpsHandler, _clipboardManager.GetClipboard(), copyNames)
                );
        }
        else if (_clipboardManager.IsCutOperation)
        {
            FileSystemEntry[] clipBoard = _clipboardManager.GetClipboard();
            if (_clipboardManager.Paste(CurrentDir, out errorMessage))
                _undoRedoHistory.AddNewNode(
                    new MoveFilesTo(_fileOpsHandler, clipBoard, CurrentDir)
                );
        }
        else
        {
            if (_clipboardManager.Paste(CurrentDir, out errorMessage))
                _undoRedoHistory.AddNewNode(
                    new CopyFilesTo(_fileOpsHandler, _clipboardManager.GetClipboard(), CurrentDir)
                );
        }
        ErrorMessage = errorMessage;
        Reload();
    }

    public void CreateShortcut(FileSystemEntry entry)
    {
        _fileOpsHandler.CreateLink(entry.PathToEntry, out string? errorMessage);
        ErrorMessage = errorMessage;
        Reload();
    }

    public void ShowProperties(FileSystemEntry entry)
    {
        WindowsFileProperties.ShowFileProperties(entry.PathToEntry, out string? errorMessage);
        ErrorMessage = errorMessage;
    }

    public void Rename(string newName)
    {
        if (SelectedFiles.Count == 1)
            RenameOne(newName);
        else if (SelectedFiles.Count > 1)
            RenameMultiple(newName);
    }

    private void RenameOne(string newName)
    {
        FileSystemEntry entry = SelectedFiles[0];
        bool result = entry.IsDirectory
            ? _fileOpsHandler.RenameDirAt(entry.PathToEntry, newName, out string? errorMessage)
            : _fileOpsHandler.RenameFileAt(entry.PathToEntry, newName, out errorMessage);

        if (result)
        {
            _undoRedoHistory.AddNewNode(new RenameOne(_fileOpsHandler, entry, newName));
            Reload();
            SelectedFiles.Add(FileEntries.First(entry => entry.Name == newName));
        }
        else
            ErrorMessage = errorMessage;
    }

    private void RenameMultiple(string namingPattern)
    {
        bool onlyFiles = !SelectedFiles[0].IsDirectory;
        string extension = onlyFiles
            ? Path.GetExtension(SelectedFiles[0].PathToEntry)
            : string.Empty;

        if (!FileNameGenerator.CanBeRenamedCollectively(SelectedFiles, onlyFiles, extension))
        {
            ErrorMessage = "Selected entries aren't of the same type.";
            return;
        }

        string[] newNames = FileNameGenerator.GetAvailableNames(SelectedFiles, namingPattern);
        bool errorOccured = false;
        for (int i = 0; i < SelectedFiles.Count; i++)
        {
            string path = SelectedFiles[i].PathToEntry;
            bool result = onlyFiles
                ? _fileOpsHandler.RenameFileAt(path, newNames[i], out string? errorMessage)
                : _fileOpsHandler.RenameDirAt(path, newNames[i], out errorMessage);

            errorOccured = !result || errorOccured;
            if (!result)
                ErrorMessage = errorMessage;
        }
        if (!errorOccured)
            _undoRedoHistory.AddNewNode(
                new RenameMultiple(_fileOpsHandler, SelectedFiles.ToArray(), newNames)
            );
        Reload();
    }

    public void MoveToTrash()
    {
        bool errorOccured = false;
        foreach (FileSystemEntry entry in SelectedFiles)
        {
            bool result = entry.IsDirectory
                ? _fileOpsHandler.MoveDirToTrash(entry.PathToEntry, out string? errorMessage)
                : _fileOpsHandler.MoveFileToTrash(entry.PathToEntry, out errorMessage);

            errorOccured = !result || errorOccured;
            ErrorMessage = errorMessage;
        }
        if (!errorOccured)
            _undoRedoHistory.AddNewNode(
                new MoveFilesToTrash(_fileOpsHandler, SelectedFiles.ToArray())
            );
        Reload();
    }

    public void Delete()
    {
        foreach (FileSystemEntry entry in SelectedFiles)
        {
            string? errorMessage;
            if (entry.IsDirectory)
                _fileOpsHandler.DeleteDir(entry.PathToEntry, out errorMessage);
            else
                _fileOpsHandler.DeleteFile(entry.PathToEntry, out errorMessage);

            ErrorMessage = errorMessage;
        }
        Reload();
    }

    private void SortByName()
    {
        SortReversed = SortBy == SortBy.Name && !SortReversed;
        SortBy = SortBy.Name;
        Reload();
    }

    private void SortByDate()
    {
        SortReversed = SortBy == SortBy.Date && !SortReversed;
        SortBy = SortBy.Date;
        Reload();
    }

    private void SortByType()
    {
        SortReversed = SortBy == SortBy.Type && !SortReversed;
        SortBy = SortBy.Type;
        Reload();
    }

    private void SortBySize()
    {
        SortReversed = SortBy == SortBy.Size && !SortReversed;
        SortBy = SortBy.Size;
        Reload();
    }

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
                ErrorMessage = errorMessage;

            _undoRedoHistory.RemoveNode(true);
        }
    }

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
                ErrorMessage = errorMessage;

            _undoRedoHistory.RemoveNode(true);
        }
    }

    private void SelectAll()
    {
        SelectedFiles.Clear();

        foreach (FileSystemEntry entry in FileEntries)
            SelectedFiles.Add(entry);
    }

    private void SelectNone() => SelectedFiles.Clear();

    private void InvertSelection()
    {
        if (SelectedFiles.Count <= ArraySearchThreshold)
        {
            string[] oldSelectionNames = new string[SelectedFiles.Count];
            for (int i = 0; i < SelectedFiles.Count; i++)
                oldSelectionNames[i] = SelectedFiles[i].Name;

            SelectedFiles.Clear();
            foreach (FileSystemEntry entry in FileEntries)
            {
                if (!oldSelectionNames.Contains(entry.Name))
                    SelectedFiles.Add(entry);
            }
        }
        else
        {
            HashSet<string> oldSelectionNames = SelectedFiles
                .Select(entry => entry.Name)
                .ToHashSet();

            SelectedFiles.Clear();
            foreach (FileSystemEntry entry in FileEntries)
            {
                if (!oldSelectionNames.Contains(entry.Name))
                    SelectedFiles.Add(entry);
            }
        }
    }

    public void StageFile(FileSystemEntry entry)
    {
        _versionControl.StageChange(entry.PathToEntry, out string? errorMessage);
        ErrorMessage = errorMessage;
    }

    public void UnstageFile(FileSystemEntry entry)
    {
        _versionControl.UnstageChange(entry.PathToEntry, out string? errorMessage);
        ErrorMessage = errorMessage;
    }

    private void Pull()
    {
        if (!IsVersionControlled)
            return;

        if (_versionControl.DownloadChanges(out string? errorMessage))
            Reload();

        ErrorMessage = errorMessage;
    }

    public void Commit(string commitMessage)
    {
        if (!IsVersionControlled)
            return;

        if (_versionControl.CommitChanges(commitMessage, out string? errorMessage))
            Reload();

        ErrorMessage = errorMessage;
    }

    private void Push()
    {
        _versionControl.UploadChanges(out string? errorMessage);
        ErrorMessage = errorMessage;
    }

    public void DisposeResources()
    {
        _versionControl.Dispose();
        _searchCTS.Dispose();
        _refreshTimer?.Stop();
    }
}
#pragma warning restore CA1822 // Mark members as static
