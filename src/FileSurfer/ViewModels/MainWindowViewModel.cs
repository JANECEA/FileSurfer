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
using FileSurfer.UndoableFileOperations;
using FileSurfer.Views;
using ReactiveUI;

namespace FileSurfer.ViewModels;

enum SortBy
{
    Name,
    Date,
    Type,
    Size
}

#pragma warning disable CA1822 // Mark members as static
public class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
{
    private const string ThisPCLabel = "This PC";
    private const string SearchingLabel = "Search Results";
    private const string NewFileName = "New File";
    private const string NewDirName = "New Folder";
    private const string NewImageName = "New Image.png";
    private const long ShowDialogLimitB = 262144000; // 250 MiB
    private const int ArraySearchThreshold = 25;

    private readonly IFileOperationsHandler _fileOpsHandler = new WindowsFileOperationsHandler(
        ShowDialogLimitB
    );
    private readonly IVersionControl _versionControl;
    private readonly UndoRedoHandler<IUndoableFileOperation> _undoRedoHistory = new();
    private readonly UndoRedoHandler<string> _pathHistory = new();
    private readonly ClipboardManager _clipboardManager;

    private CancellationTokenSource _searchCTS = new();
    private bool _isUserInvoked = true;
    private bool _sortReversed = false;
    private SortBy _sortBy = SortBy.Name;

    private readonly ObservableCollection<FileSystemEntry> _selectedFiles = new();
    public ObservableCollection<FileSystemEntry> SelectedFiles => _selectedFiles;
    private readonly ObservableCollection<FileSystemEntry> _fileEntries = new();
    public ObservableCollection<FileSystemEntry> FileEntries => _fileEntries;

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
            new ErrorWindow(errorMessage).Show();
        });

    private string _currentDir = "D:\\Stažené\\testing";
    public string CurrentDir
    {
        get => _currentDir;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentDir, value);
            if (IsValidDirectory(value))
            {
                if (Searching)
                    CancelSearch(value);

                Reload();

                if (_isUserInvoked && value != _pathHistory.Current)
                    _pathHistory.AddNewNode(value);
            }
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
    public ReactiveCommand<Unit, Unit> PullCommand { get; }
    public ReactiveCommand<Unit, Unit> PushCommand { get; }

    public MainWindowViewModel()
    {
        _selectedFiles.CollectionChanged += UpdateSelectionInfo;
        _versionControl = new GitVersionControlHandler(_fileOpsHandler);
        _clipboardManager = new ClipboardManager(_fileOpsHandler, NewImageName);
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
        PullCommand = ReactiveCommand.Create(Pull);
        PushCommand = ReactiveCommand.Create(Push);
        Reload();
        _pathHistory.AddNewNode(CurrentDir);
    }

    private void Reload()
    {
        if (Searching)
            return;

        if (CurrentDir == ThisPCLabel)
        {
            LoadDrives();
            SetDriveInfo();
        }
        else
        {
            LoadDirEntries();
            UpdateSelectionInfo();
        }
        CheckDirectoryEmpty();
        SetSearchWaterMark();
        CheckVersionContol();
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
        SelectionInfo = FileEntries.Count == 1 ? "1 drive" : $"{FileEntries.Count} drives";

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
        else
        {
            _fileOpsHandler.OpenFile(entry.PathToEntry, out string? errorMessage);
            ErrorMessage = errorMessage;
        }
    }

    public void GoUp()
    {
        if (Path.GetDirectoryName(CurrentDir) is not string parentDir)
            CurrentDir = ThisPCLabel;
        else if (CurrentDir != ThisPCLabel)
            CurrentDir = parentDir;
    }

    private void LoadDrives()
    {
        FileEntries.Clear();
        foreach (DriveInfo drive in _fileOpsHandler.GetDrives())
            FileEntries.Add(new FileSystemEntry(drive));
    }

    private void LoadDirEntries()
    {
        string[] dirPaths = _fileOpsHandler.GetPathDirs(CurrentDir, true, false);
        FileSystemEntry[] directories = new FileSystemEntry[dirPaths.Length];
        for (int i = 0; i < dirPaths.Length; i++)
            directories[i] = new FileSystemEntry(dirPaths[i], true, _fileOpsHandler);

        string[] filePaths = _fileOpsHandler.GetPathFiles(CurrentDir, true, false);
        FileSystemEntry[] files = new FileSystemEntry[filePaths.Length];
        for (int i = 0; i < filePaths.Length; i++)
            files[i] = new FileSystemEntry(filePaths[i], false, _fileOpsHandler);

        SortAndAddEntries(directories, files);
    }

    private void SortAndAddEntries(FileSystemEntry[] directories, FileSystemEntry[] files)
    {
        if (_sortBy is not SortBy.Name)
            SortInPlace(files, _sortBy);

        if (_sortBy is not SortBy.Name and not SortBy.Type and not SortBy.Size)
            SortInPlace(directories, _sortBy);

        FileEntries.Clear();

        if (!_sortReversed || _sortBy is SortBy.Type or SortBy.Size)
            for (int i = 0; i < directories.Length; i++)
                FileEntries.Add(directories[i]);
        else
            for (int i = directories.Length - 1; i >= 0; i--)
                FileEntries.Add(directories[i]);

        if (!_sortReversed)
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
                Array.Sort(entries, (x, y) => DateTime.Compare(y.LastChanged, x.LastChanged));
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
            Directory.Exists(CurrentDir) && _versionControl.IsVersionControlled(CurrentDir);

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
            _searchCTS = new();

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
            foreach (string file in await GetPathFilesAsync(directory, true, false, searchQuery))
                if (!searchCTS.IsCancellationRequested)
                    FileEntries.Add(new FileSystemEntry(file, false, _fileOpsHandler));

            foreach (string dir in await GetPathDirsAsync(directory, true, false, searchQuery))
                if (!searchCTS.IsCancellationRequested)
                    FileEntries.Add(new FileSystemEntry(dir, true, _fileOpsHandler));
        });

        foreach (string dir in await GetPathDirsAsync(directory, true, false))
            if (!searchCTS.IsCancellationRequested)
                await SearchDirectoryAsync(dir, searchQuery, searchCTS);
    }

    private async Task<IEnumerable<string>> GetPathFilesAsync(
        string directory,
        bool includeHidden,
        bool includeOS,
        string searchQuery
    )
    {
        IEnumerable<string> entries = await Task.Run(
            () => _fileOpsHandler.GetPathFiles(directory, includeHidden, includeOS)
        );
        return entries.Where(name => Path.GetFileName(name).Contains(searchQuery));
    }

    private async Task<IEnumerable<string>> GetPathDirsAsync(
        string directory,
        bool includeHidden,
        bool includeOS,
        string? searchQuery = null
    )
    {
        IEnumerable<string> entries = await Task.Run(
            () => _fileOpsHandler.GetPathDirs(directory, includeHidden, includeOS)
        );
        return searchQuery is null
            ? entries
            : entries.Where(name => Path.GetFileName(name).Contains(searchQuery));
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

    private void Cut()
    {
        _clipboardManager.Cut(SelectedFiles.ToList(), CurrentDir, out string? errorMessage);
        ErrorMessage = errorMessage;
    }

    private void Copy()
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
            if ( _clipboardManager.Paste(CurrentDir, out errorMessage))
                _undoRedoHistory.AddNewNode(
                    new CopyFilesTo(_fileOpsHandler, _clipboardManager.GetClipboard(), CurrentDir)
                );
        }
        ErrorMessage = errorMessage;
        Reload();
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
            FileSystemEntry entry = SelectedFiles[i];
            string? errorMessage;
            bool result = onlyFiles
                ? _fileOpsHandler.RenameFileAt(entry.PathToEntry, newNames[i], out errorMessage)
                : _fileOpsHandler.RenameDirAt(entry.PathToEntry, newNames[i], out errorMessage);

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

    private void MoveToTrash()
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

    private void Delete()
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
        _sortReversed = _sortBy == SortBy.Name && !_sortReversed;
        _sortBy = SortBy.Name;
        Reload();
    }

    private void SortByDate()
    {
        _sortReversed = _sortBy == SortBy.Date && !_sortReversed;
        _sortBy = SortBy.Date;
        Reload();
    }

    private void SortByType()
    {
        _sortReversed = _sortBy == SortBy.Type && !_sortReversed;
        _sortBy = SortBy.Type;
        Reload();
    }

    private void SortBySize()
    {
        _sortReversed = _sortBy == SortBy.Size && !_sortReversed;
        _sortBy = SortBy.Size;
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
            _undoRedoHistory.RemoveNode(true);
            ErrorMessage = errorMessage;
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
            _undoRedoHistory.RemoveNode(true);
            ErrorMessage = errorMessage;
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
}
#pragma warning restore CA1822 // Mark members as static
