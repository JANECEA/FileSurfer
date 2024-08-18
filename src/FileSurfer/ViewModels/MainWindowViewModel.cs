using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
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
    Type
}

#pragma warning disable CA1822 // Mark members as static
public class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
{
    private const int ArraySearchThreshold = 25;
    private const string ThisComputer = "This PC";
    private const string SearchingDirectory = "Search Results";
    private const string NewFileName = "New File";
    private const string NewDirName = "New Folder";

    private readonly IFileOperationsHandler _fileOpsHandler = new WindowsFileOperationsHandler();
    private readonly IVersionControl _versionControl;
    private readonly UndoRedoHandler<IUndoableFileOperation> _undoRedoHistory = new();
    private readonly UndoRedoHandler<string> _pathHistory = new();
    private List<FileSystemEntry> _programClipboard = new();
    private bool _isCutOperation;
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
            if (value is not null && value != string.Empty)
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

    private string _currentDir = ThisComputer;
    public string CurrentDir
    {
        get => _currentDir;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentDir, value);
            if (IsValidDirectory(value))
            {
                Reload();
                if (_isUserInvoked)
                    _pathHistory.NewNode(value);
            }
        }
    }

    private bool _directoryEmpty;
    public bool DirectoryEmpty
    {
        get => _directoryEmpty;
        set => this.RaiseAndSetIfChanged(ref _directoryEmpty, value);
    }

    private bool _searching;
    public bool Searching
    {
        get => _searching;
        set => this.RaiseAndSetIfChanged(ref _searching, value);
    }

    private string[] _branches = Array.Empty<string>();
    public string[] Branches
    {
        get => _branches;
        set => this.RaiseAndSetIfChanged(ref _branches, value);
    }

    private string? _currentBranch;
    public string? CurrentBranch
    {
        get => _currentBranch;
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                _versionControl.SwitchBranches(value, out string? errorMessage);
                ErrorMessage = errorMessage;
            }
            _currentBranch = value;
        }
    }

    private bool _isVersionControlled = false;
    public bool IsVersionControlled
    {
        get => _isVersionControlled;
        set => this.RaiseAndSetIfChanged(ref _isVersionControlled, value);
    }

    private string _selectionInfo = string.Empty;
    public string SelectionInfo
    {
        get => _selectionInfo;
        set => this.RaiseAndSetIfChanged(ref _selectionInfo, value);
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
        UndoCommand = ReactiveCommand.Create(Undo);
        RedoCommand = ReactiveCommand.Create(Redo);
        SelectAllCommand = ReactiveCommand.Create(SelectAll);
        SelectNoneCommand = ReactiveCommand.Create(SelectNone);
        InvertSelectionCommand = ReactiveCommand.Create(InvertSelection);
        PullCommand = ReactiveCommand.Create(Pull);
        PushCommand = ReactiveCommand.Create(Push);
        Reload();
        _pathHistory.NewNode(_currentDir);
    }

    private void Reload()
    {
        if (_currentDir == ThisComputer)
        {
            LoadDrives();
            return;
        }
        LoadDirEntries();
        CheckDirectoryEmpty();
        UpdateSelectionInfo();
        CheckVersionContol();
    }

    public int GetNameEndIndex(FileSystemEntry entry) =>
        _selectedFiles.Count > 0
            ? Path.GetFileNameWithoutExtension(entry.PathToEntry).Length
            : 0;

    private bool IsValidDirectory(string path) =>
        path == ThisComputer || Directory.Exists(path);

    private void UpdateSelectionInfo(
        object? sender = null,
        NotifyCollectionChangedEventArgs? e = null
    )
    {
        string selectionInfo = _fileEntries.Count == 1 ? "1 item" : $"{_fileEntries.Count} items";

        if (_selectedFiles.Count == 1)
            selectionInfo += $"  |  1 item selected";
        else if (_selectedFiles.Count > 1)
            selectionInfo += $"  |  {_selectedFiles.Count} items selected";

        bool displaySize = _selectedFiles.Count >= 1;
        long sizeSum = 0;
        foreach (FileSystemEntry entry in _selectedFiles)
        {
            if (entry.IsDirectory)
            {
                displaySize = false;
                break;
            }

            if (entry.SizeKib is long sizeKiB)
                sizeSum += sizeKiB;
        }
        if (displaySize)
            selectionInfo += $"  {sizeSum} KiB";

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
        if (Path.GetDirectoryName(CurrentDir) is not string dirName)
            CurrentDir = ThisComputer;

        else if (CurrentDir != ThisComputer)
            CurrentDir = dirName;
    }

    private void LoadDrives()
    {
        _fileEntries.Clear();
        foreach (DriveInfo drive in _fileOpsHandler.GetDrives())
            _fileEntries.Add(new FileSystemEntry(drive.Name, drive.VolumeLabel));
    }

    private void LoadDirEntries()
    {
        string[] dirPaths = _fileOpsHandler.GetPathDirs(_currentDir, true, false);
        FileSystemEntry[] directories = new FileSystemEntry[dirPaths.Length];
        for (int i = 0; i < dirPaths.Length; i++)
            directories[i] = new FileSystemEntry(dirPaths[i], true, _fileOpsHandler);

        string[] filePaths = _fileOpsHandler.GetPathFiles(_currentDir, true, false);
        FileSystemEntry[] files = new FileSystemEntry[filePaths.Length];
        for (int i = 0; i < filePaths.Length; i++)
            files[i] = new FileSystemEntry(filePaths[i], false, _fileOpsHandler);

        SortAndAddEntries(directories, files);
    }

    private void SortAndAddEntries(FileSystemEntry[] directories, FileSystemEntry[] files)
    {
        if (_sortBy is not SortBy.Name)
            SortInPlace(files, _sortBy);

        if (_sortBy is not SortBy.Name and not SortBy.Type)
            SortInPlace(directories, _sortBy);

        _fileEntries.Clear();
        if (!_sortReversed)
        {
            for (int i = 0; i < directories.Length; i++)
                _fileEntries.Add(directories[i]);

            for (int i = 0; i < files.Length; i++)
                _fileEntries.Add(files[i]);
        }
        else
        {
            for (int i = directories.Length - 1; i >= 0; i--)
                _fileEntries.Add(directories[i]);

            for (int i = files.Length - 1; i >= 0; i--)
                _fileEntries.Add(files[i]);
        }
    }

    private void SortInPlace(FileSystemEntry[] entries, SortBy sortBy)
    {
        switch (sortBy)
        {
            case SortBy.Name:
                Array.Sort(entries, (x, y) => string.Compare(x.Name, y.Name));
                break;

            case SortBy.Date:
                Array.Sort(entries, (x, y) => DateTime.Compare(x.LastChanged, y.LastChanged));
                break;

            case SortBy.Type:
                Array.Sort(entries, (x, y) => string.Compare(x.Type, y.Type));
                break;

            default:
                throw new ArgumentException($"Unsupported sort option: {sortBy}", nameof(sortBy));
        }
    }

    private void CheckVersionContol() =>
        IsVersionControlled = _versionControl.IsVersionControlled(_currentDir);

    private void CheckDirectoryEmpty() => DirectoryEmpty = _fileEntries.Count == 0;

    public void GoBack()
    {
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
        if (_currentDir == ThisComputer)
            return;

        _fileOpsHandler.OpenCmdAt(_currentDir, out string? errorMessage);
        ErrorMessage = errorMessage;
    }

    public async void SearchRelay(string searchQuerry)
    {
        string currentDir = CurrentDir;
        CurrentDir = SearchingDirectory;
        _fileEntries.Clear();
        Searching = true;
        await SearchDirectoryAsync(currentDir, searchQuerry);
    }

    private async Task SearchDirectoryAsync(string directory ,string searchQuery) 
    {
        if (!Searching)
            return;
        
        string[] files = _fileOpsHandler.GetPathFiles(directory, true, false);
        foreach (string file in files)
            _fileEntries.Add(new FileSystemEntry(file, false, _fileOpsHandler));

        string[] directories = _fileOpsHandler.GetPathDirs(directory, true, false);
        foreach (string dir in directories)
            _fileEntries.Add(new FileSystemEntry(dir, true, _fileOpsHandler));

        foreach (string dir in _fileOpsHandler.GetPathDirs(directory, true, false))
            await SearchDirectoryAsync(dir, searchQuery);
    }

    public void CancelSearch()
    {
        Searching = false;
        CurrentDir = _pathHistory.Current ?? ThisComputer;
    }

    private void NewFile()
    {
        string newFileName = FileNameGenerator.GetAvailableName(_currentDir, NewFileName);
        if (_fileOpsHandler.NewFileAt(_currentDir, newFileName, out string? errorMessage))
        {
            Reload();
            _undoRedoHistory.NewNode(new NewFileAt(_fileOpsHandler, _currentDir, newFileName));
            _selectedFiles.Add(_fileEntries.First(entry => entry.Name == newFileName));
        }
        else
            ErrorMessage = errorMessage;
    }

    private void NewDir()
    {
        string newDirName = FileNameGenerator.GetAvailableName(_currentDir, NewDirName);
        if (_fileOpsHandler.NewDirAt(_currentDir, newDirName, out string? errorMessage))
        {
            Reload();
            _undoRedoHistory.NewNode(new NewDirAt(_fileOpsHandler, _currentDir, newDirName));
            _selectedFiles.Add(_fileEntries.First(entry => entry.Name == newDirName));
        }
        else
            ErrorMessage = errorMessage;
    }

    private void Cut()
    {
        if (
            _fileOpsHandler.CopyToOSClipBoard(
                _selectedFiles.Select(entry => entry.PathToEntry).ToArray(),
                out string? errorMessage
            )
        )
        {
            _isCutOperation = true;
            _programClipboard = _selectedFiles.ToList();
        }
        else
            ErrorMessage = errorMessage;
    }

    private void Copy()
    {
        if (
            _fileOpsHandler.CopyToOSClipBoard(
                _selectedFiles.Select(entry => entry.PathToEntry).ToArray(),
                out string? errorMessage
            )
        )
        {
            _isCutOperation = false;
            _programClipboard = _selectedFiles.ToList();
        }
        else
            ErrorMessage = errorMessage;
    }

    private void Paste()
    {
        if (!_fileOpsHandler.PasteFromOSClipBoard(_currentDir, out string? errorMessage))
        {
            ErrorMessage = errorMessage;
            _programClipboard.Clear();
            Reload();
            return;
        }

        if (_isCutOperation)
        {
            _undoRedoHistory.NewNode(
                new MoveFilesTo(_fileOpsHandler, _programClipboard.ToArray(), _currentDir)
            );
            foreach (FileSystemEntry entry in _programClipboard)
            {
                if (entry.IsDirectory)
                    _fileOpsHandler.DeleteDir(entry.PathToEntry, out errorMessage);
                else
                    _fileOpsHandler.DeleteFile(entry.PathToEntry, out errorMessage);
            }
            _programClipboard.Clear();
        }
        else
            _undoRedoHistory.NewNode(
                new CopyFilesTo(_fileOpsHandler, _programClipboard.ToArray(), _currentDir)
            );
        Reload();
    }

    public void Rename(string newName)
    {
        if (_selectedFiles.Count == 1)
            RenameOne(newName);
        else if (_selectedFiles.Count > 1)
            RenameMultiple(newName);
    }

    private void RenameOne(string newName)
    {
        FileSystemEntry entry = _selectedFiles[0];
        bool result = entry.IsDirectory
            ? _fileOpsHandler.RenameDirAt(entry.PathToEntry, newName, out string? errorMessage)
            : _fileOpsHandler.RenameFileAt(entry.PathToEntry, newName, out errorMessage);

        if (result)
        {
            _undoRedoHistory.NewNode(new RenameOne(_fileOpsHandler, entry, newName));
            Reload();
            _selectedFiles.Add(_fileEntries.First(entry => entry.Name == newName));
        }
        else
            ErrorMessage = errorMessage;
    }

    private void RenameMultiple(string namingPattern)
    {
        bool onlyFiles = !_selectedFiles[0].IsDirectory;
        string extension = onlyFiles
            ? Path.GetExtension(_selectedFiles[0].PathToEntry)
            : string.Empty;

        if (!FileNameGenerator.CanBeRenamed(_selectedFiles, onlyFiles, extension))
        {
            ErrorMessage = "Selected entries aren't of the same type.";
            return;
        }

        string[] newNames = FileNameGenerator.GetAvailableNames(_selectedFiles, namingPattern);
        bool errorOccured = false;
        for (int i = 0; i < _selectedFiles.Count; i++)
        {
            FileSystemEntry entry = _selectedFiles[i];
            string? errorMessage;
            bool result = onlyFiles
                ? _fileOpsHandler.RenameFileAt(entry.PathToEntry, newNames[i], out errorMessage)
                : _fileOpsHandler.RenameDirAt(entry.PathToEntry, newNames[i], out errorMessage);

            errorOccured = !result || errorOccured;
            if (!result)
                ErrorMessage = errorMessage;
        }
        if (!errorOccured)
            _undoRedoHistory.NewNode(
                new RenameMultiple(_fileOpsHandler, _selectedFiles.ToArray(), newNames)
            );
        Reload();
    }

    private void MoveToTrash()
    {
        bool errorOccured = false;
        foreach (FileSystemEntry entry in _selectedFiles)
        {
            bool result = entry.IsDirectory
                ? _fileOpsHandler.MoveDirToTrash(entry.PathToEntry, out string? errorMessage)
                : _fileOpsHandler.MoveFileToTrash(entry.PathToEntry, out errorMessage);

            errorOccured = !result || errorOccured;
            ErrorMessage = errorMessage;
        }
        if (!errorOccured)
            _undoRedoHistory.NewNode(
                new MoveFilesToTrash(_fileOpsHandler, _selectedFiles.ToArray())
            );
        Reload();
    }

    private void Delete()
    {
        foreach (FileSystemEntry entry in _selectedFiles)
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
        _selectedFiles.Clear();

        foreach (FileSystemEntry entry in _fileEntries)
            _selectedFiles.Add(entry);
    }

    private void SelectNone() => SelectedFiles.Clear();

    private void InvertSelection()
    {
        if (_selectedFiles.Count <= ArraySearchThreshold)
        {
            string[] oldSelectionNames = _selectedFiles.Select(entry => entry.Name).ToArray();

            _selectedFiles.Clear();
            foreach (FileSystemEntry entry in _fileEntries)
            {
                if (!oldSelectionNames.Contains(entry.Name))
                    _selectedFiles.Add(entry);
            }
        }
        else
        {
            HashSet<string> oldSelectionNames = _selectedFiles
                .Select(entry => entry.Name)
                .ToHashSet();

            _selectedFiles.Clear();
            foreach (FileSystemEntry entry in _fileEntries)
            {
                if (!oldSelectionNames.Contains(entry.Name))
                    _selectedFiles.Add(entry);
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
