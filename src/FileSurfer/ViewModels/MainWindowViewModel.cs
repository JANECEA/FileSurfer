using System;
using System.Collections;
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

    private readonly IFileOperationsHandler _fileOpsHandler = new WindowsFileOperationsHandler();
    private readonly UndoRedoHandler<IUndoableFileOperation> _undoRedoHistory = new();
    private readonly UndoRedoHandler<string> _pathHistory = new();
    private readonly IVersionControl _versionControl;
    private bool _isUserInvoked = true;
    private SortBy _sortBy = SortBy.Name;
    private bool _sortReversed = false;
    private List<FileSystemEntry> _programClipboard = new();
    private bool _isCutOperation;

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

    private string _currentDir = "D:\\Stažené\\";
    public string CurrentDir
    {
        get => _currentDir;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentDir, value);
            if (Directory.Exists(value))
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

    private bool _newNameRequired = false;
    public bool NewNameRequired
    {
        get => _newNameRequired;
        set => this.RaiseAndSetIfChanged(ref _newNameRequired, value);
    }

    private bool _commitMessageRequired = false;
    public bool CommitMessageRequired
    {
        get => _commitMessageRequired;
        set => this.RaiseAndSetIfChanged(ref _commitMessageRequired, value);
    }

    private string _newNameBox = string.Empty;
    public string NewNameBox
    {
        get => _newNameBox;
        set => this.RaiseAndSetIfChanged(ref _newNameBox, value);
    }

    private string _commitMessageBox = string.Empty;
    public string CommitMessageBox
    {
        get => _commitMessageBox;
        set => this.RaiseAndSetIfChanged(ref _commitMessageBox, value);
    }

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value;
            if (!string.IsNullOrEmpty(value))
            {
                SearchDirectory(_searchQuery);
            }
        }
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
        _pathHistory.NewNode(CurrentDir);
    }

    private void Reload()
    {
        LoadDirEntries();
        CheckDirectoryEmpty();
        UpdateSelectionInfo();
        CheckVersionContol();
    }

    public int GetSelectedNameEndIndex() => 
        _selectedFiles.Count > 0
        ? Path.GetFileNameWithoutExtension(_selectedFiles[0].PathToEntry).Length
        : 0;

    private void UpdateSelectionInfo(object? sender = null, NotifyCollectionChangedEventArgs? e = null)
    {
        string selectionInfo = _fileEntries.Count == 1
            ? "1 item"
            :$"{_fileEntries.Count} items";

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
        if (Path.GetDirectoryName(CurrentDir) is string dirName)
            CurrentDir = dirName;
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

    private void CheckDirectoryEmpty() =>
        DirectoryEmpty = _fileEntries.Count == 0;

    public void GoBack()
    {
        if (_pathHistory.GetPrevious() is string previousPath)
        {
            _pathHistory.MoveToPrevious();

            if (Path.Exists(previousPath))
            {
                _isUserInvoked = false;
                CurrentDir = previousPath;
                _isUserInvoked = true;
            }
            else
                _pathHistory.RemoveNode(false);
        }
    }

    public void GoForward()
    {
        if (_pathHistory.GetNext() is string nextPath)
        {
            _pathHistory.MoveToNext();

            if (Path.Exists(nextPath))
            {
                _isUserInvoked = false;
                CurrentDir = nextPath;
                _isUserInvoked = true;
            }
            else
                _pathHistory.RemoveNode(true);
        }
    }

    private void OpenPowerShell()
    {
        _fileOpsHandler.OpenCmdAt(_currentDir, out string? errorMessage);
        ErrorMessage = errorMessage;
    }

    private void SearchDirectory(string searchQuery) { }

    private void CancelSearch() { }

    private void NewFile()
    {
        string newFileName = FileNameGenerator.GetAvailableName(_currentDir, "New File");
        if (_fileOpsHandler.NewFileAt(_currentDir, newFileName, out string? errorMessage))
        {
            Reload();
            _undoRedoHistory.NewNode(new NewFileAt(_fileOpsHandler, _currentDir, newFileName));
        }
        else
            ErrorMessage = errorMessage;
    }

    private void NewDir()
    {
        string newDirName = FileNameGenerator.GetAvailableName(_currentDir, "New Folder");
        if (_fileOpsHandler.NewDirAt(_currentDir, newDirName, out string? errorMessage))
        {
            Reload();
            _undoRedoHistory.NewNode(new NewDirAt(_fileOpsHandler, _currentDir, newDirName));
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

    public void RenameRelay()
    {
        if (_selectedFiles.Count == 0)
            return;

        NewNameRequired = true;
        NewNameBox = _selectedFiles[0].Name;
    }

    private void Rename()
    {
        if (_selectedFiles.Count == 1)
            RenameOne();
        else if (_selectedFiles.Count > 1)
            RenameMultiple();

        Reload();
    }

    public void RenameOne()
    {
        string? errorMessage;
        bool result;
        if (_selectedFiles[0].IsDirectory)
        {
            result = _fileOpsHandler.RenameDirAt(
                _selectedFiles[0].PathToEntry,
                "New Name",
                out errorMessage
            );
        }
        else
        {
            result = _fileOpsHandler.RenameFileAt(
                _selectedFiles[0].PathToEntry,
                "New Name",
                out errorMessage
            );
        }
        if (result)
            _undoRedoHistory.NewNode(new RenameOne(_fileOpsHandler, _selectedFiles[0], "New Name"));
        else
            ErrorMessage = errorMessage;
    }

    public void RenameMultiple()
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

        string[] newNames = FileNameGenerator.GetAvailableNames(
            _selectedFiles,
            "New Naming Pattern"
        );
        bool errorOccured = false;
        for (int i = 0; i < _selectedFiles.Count; i++)
        {
            bool result = onlyFiles
                ? _fileOpsHandler.RenameFileAt(
                    _selectedFiles[i].PathToEntry,
                    newNames[i],
                    out string? errorMessage
                )
                : _fileOpsHandler.RenameDirAt(
                    _selectedFiles[i].PathToEntry,
                    newNames[i],
                    out errorMessage
                );

            errorOccured = !result || errorOccured;
            if (!result)
                ErrorMessage = errorMessage;
        }
        if (!errorOccured)
            _undoRedoHistory.NewNode(
                new RenameMultiple(_fileOpsHandler, _selectedFiles.ToArray(), newNames)
            );
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

    private void Commit()
    {
        if (!IsVersionControlled)
            return;

        if (_versionControl.CommitChanges("Propriatery commit message", out string? errorMessage))
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
