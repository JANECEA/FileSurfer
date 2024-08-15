using Avalonia.Threading;
using FileSurfer.UndoableFileOperations;
using FileSurfer.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

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
    private SortBy _sortBy = SortBy.Name;
    private bool _sortReversed = false;
    private bool _isUserInvoked = true;

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

    private string _currentDir = "D:\\Stažené";
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

    private string _selectionInfo = "Hello World!";
    public string SelectionInfo
    {
        get => _selectionInfo;
        set => this.RaiseAndSetIfChanged(ref _selectionInfo, value);
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
    public ReactiveCommand<Unit, Unit> RenameCommand { get; }
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
    public ReactiveCommand<Unit, Unit> CommitCommand { get; }
    public ReactiveCommand<Unit, Unit> PushCommand { get; }

    public MainWindowViewModel()
    {
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
        RenameCommand = ReactiveCommand.Create(Rename);
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
        CommitCommand = ReactiveCommand.Create(Commit);
        PushCommand = ReactiveCommand.Create(Push);
        LoadDirEntries();
        CheckVC();
        _pathHistory.NewNode(CurrentDir);
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

    public void GoUp() => CurrentDir = Path.GetDirectoryName(CurrentDir) ?? CurrentDir;

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

        SortAndAdd(directories, files);
    }

    private void SortAndAdd(FileSystemEntry[] directories, FileSystemEntry[] files)
    {
        if (_sortBy is not SortBy.Name)
            SortInPlaceBy(files, _sortBy);

        if (_sortBy is not SortBy.Name and not SortBy.Type)
            SortInPlaceBy(directories, _sortBy);

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

    private void SortInPlaceBy(FileSystemEntry[] entries, SortBy sortBy)
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

    private void CheckVC() =>
        IsVersionControlled = _versionControl.IsVersionControlled(_currentDir);

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

    private void Reload()
    {
        LoadDirEntries();
        CheckVC();
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

    private void Cut() { }

    private void Copy() { }

    private void Paste() { }

    private void Rename()
    {
        string? errorMessage = null;

        if (_selectedFiles.Count == 1)
            RenameOne(out errorMessage);
        else if (_selectedFiles.Count > 1)
            RenameMultiple(ref errorMessage);

        ErrorMessage = errorMessage;
        Reload();
    }

    private void RenameOne(out string? errorMessage)
    {
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
    }

    private void RenameMultiple(ref string? errorMessage)
    {
        bool onlyDirs = _selectedFiles[0].IsDirectory;
        string extension = onlyDirs
            ? string.Empty
            : Path.GetExtension(_selectedFiles[0].PathToEntry);

        foreach (FileSystemEntry entry in _selectedFiles)
        {
            if (
                onlyDirs != entry.IsDirectory
                || (!onlyDirs && Path.GetExtension(entry.PathToEntry) != extension)
            )
            {
                ErrorMessage = "Selected entries don't have the same extensions or types.";
                return;
            }
        }
/*
        RenameMultiple operation =
            new(_fileOpsHandler, _selectedFiles.ToArray(), "New Naming Pattern");

        if (operation.Redo(out errorMessage))
            _undoRedoHistory.NewNode(operation);
*/
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
