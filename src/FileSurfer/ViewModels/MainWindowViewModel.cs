using Avalonia.Threading;
using FileSurfer.Views;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Reactive;

namespace FileSurfer.ViewModels;

#pragma warning disable CA1822 // Mark members as static
public class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
{
    private readonly IFileOperationsHandler _fileOperationsHandler = new WindowsFileOperationsHandler();
    private readonly UndoRedoHandler<IUndoableFileOperation> _undoRedoHistory = new();
    private readonly UndoRedoHandler<string> _pathHistory = new();
    private readonly IVersionControl _versionControl;

    private readonly ObservableCollection<FileSystemEntry> _selectedFiles = new();
    private ObservableCollection<FileSystemEntry> SelectedFiles => _selectedFiles;

    private readonly ObservableCollection<FileSystemEntry> _fileEntries = new();
    public ObservableCollection<FileSystemEntry> FileEntries => _fileEntries;

    private string? _errorMessage = null;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (value is not null)
            {
                this.RaiseAndSetIfChanged(ref _errorMessage, value);
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

    private string _currentDir = "D:/Stažené";
    public string CurrentDir
    {
        get => _currentDir;
        set => this.RaiseAndSetIfChanged(ref _currentDir, value);
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
    public ReactiveCommand<Unit, Unit> ListViewCommand { get; }
    public ReactiveCommand<Unit, Unit> IconViewCommand { get; }

    public MainWindowViewModel()
    {
        _versionControl = new GitVersionControlHandler(_fileOperationsHandler);
        LoadDirEntries();
        CheckVC();
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
        ListViewCommand = ReactiveCommand.Create(ListView);
        IconViewCommand = ReactiveCommand.Create(IconView);
    }

    private void LoadDirEntries()
    {
        _fileEntries.Clear();
        foreach (string dirPath in Directory.GetDirectories(_currentDir))
        {
            DirectoryInfo dirInfo = new(dirPath);
            _fileEntries.Add(new DirectoryEntry(_fileOperationsHandler, dirInfo.FullName));
        }
        foreach (string filePath in Directory.GetFiles(_currentDir))
        {
            FileInfo fileInfo = new(filePath);
            _fileEntries.Add(new FileEntry(_fileOperationsHandler, fileInfo.FullName));
        }
    }

    private void CheckVC() => 
        IsVersionControlled = _versionControl.IsVersionControlled(_currentDir);

    private void GoBack() { }

    private void GoForward() { }

    private void Reload()
    {
        LoadDirEntries();
        CheckVC();
    }

    private void OpenPowerShell()
    {
        _fileOperationsHandler.OpenCmdAt(_currentDir, out string? errorMessage);
        ErrorMessage = errorMessage;
    }

    private void SearchDirectory(string searchQuery) { }

    private void CancelSearch() { }

    private void NewFile()
    {
        string newFileName = _fileOperationsHandler.GetAvailableName(_currentDir, "New File");
        if (_fileOperationsHandler.NewFileAt(_currentDir, newFileName, out string? errorMessage))
        {
            Reload();
            _undoRedoHistory.NewNode(new NewFileAt(_fileOperationsHandler, _currentDir, newFileName));
        }
        else
            ErrorMessage = errorMessage;
    }

    private void NewDir()
    {
        string newDirName = _fileOperationsHandler.GetAvailableName(_currentDir, "New Folder");
        if (_fileOperationsHandler.NewDirAt(_currentDir, newDirName, out string? errorMessage))
        {
            Reload();
            _undoRedoHistory.NewNode(new NewDirAt(_fileOperationsHandler , _currentDir, newDirName));
        }
        else 
            ErrorMessage = errorMessage;
    }

    private void Cut() { }

    private void Copy() { }

    private void Paste() { }

    private void Rename() { }

    private void MoveToTrash() { }

    private void Delete() { }

    private void SortByName() { }

    private void SortByDate() { }

    private void SortByType() { }

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

    private void SelectAll() { }

    private void SelectNone() { }

    private void InvertSelection() { }

    private void Pull() 
    {
        if (!IsVersionControlled) 
            return;

        if (_versionControl.DownloadChanges(out string? errorMessage))
            Reload();
        else
            ErrorMessage = errorMessage;
    }

    private void Commit() { }

    private void Push() { }

    private void ListView() { }

    private void IconView() { }
}
#pragma warning restore CA1822 // Mark members as static
