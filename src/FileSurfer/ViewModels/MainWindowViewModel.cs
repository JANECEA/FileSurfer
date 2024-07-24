using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using ReactiveUI;

namespace FileSurfer.ViewModels;

public class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
{
#pragma warning disable CA1822 // Mark members as static
    private readonly IVersionControl _versionControl = new GitVersionControlHandler();
    private readonly IFileOperationsHandler _fileOperationsHandler =
        new WindowsFileOperationsHandler();
    private readonly UndoRedoHandler<IUndoableFileOperation> _undoRedoHandler = new();

    private ObservableCollection<string> _selectedFiles = new();

    private bool _needRefresh = false;
    public bool NeedRefresh
    {
        get => _needRefresh;
        set => this.RaiseAndSetIfChanged(ref _needRefresh, value);
    }

    private string? _errorMessage = null;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (value is not null)
            {
                this.RaiseAndSetIfChanged(ref _errorMessage, value);
            }
        }
    }

    private string _currentPath = "D:/Stažené";
    public string CurrentPath
    {
        get => _currentPath;
        set => this.RaiseAndSetIfChanged(ref _currentPath, value);
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
                _errorMessage = errorMessage;
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

    public ICommand GoBackCommand { get; }
    public ICommand GoForwardCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand OpenPowerShellCommand { get; }
    public ICommand CancelSearchCommand { get; }
    public ICommand NewFileCommand { get; }
    public ICommand NewFolderCommand { get; }
    public ICommand CutCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand MoveToTrashCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SortByNameCommand { get; }
    public ICommand SortByDateCommand { get; }
    public ICommand SortByTypeCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand SelectNoneCommand { get; }
    public ICommand InvertSelectionCommand { get; }
    public ICommand PullCommand { get; }
    public ICommand CommitCommand { get; }
    public ICommand PushCommand { get; }
    public ICommand ListViewCommand { get; }
    public ICommand IconViewCommand { get; }

    public MainWindowViewModel()
    {
        GoBackCommand = ReactiveCommand.Create(GoBack);
        GoForwardCommand = ReactiveCommand.Create(GoForward);
        ReloadCommand = ReactiveCommand.Create(Reload);
        OpenPowerShellCommand = ReactiveCommand.Create(OpenPowerShell);
        CancelSearchCommand = ReactiveCommand.Create(CancelSearch);
        NewFileCommand = ReactiveCommand.Create(NewFile);
        NewFolderCommand = ReactiveCommand.Create(NewFolder);
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

    private void GoBack() { }

    private void GoForward() { }

    private void Reload() { }

    private void OpenPowerShell() =>
        _fileOperationsHandler.OpenCmdAt(_currentPath, out _errorMessage);

    private void SearchDirectory(string searchQuery) { }

    private void CancelSearch() { }

    private void NewFile()
    {
        if (_fileOperationsHandler.NewFileAt(_currentPath, "New File", out _errorMessage))
        {
            _needRefresh = true;
            // _undoRedoHandler.NewOperation(new NewFileAt(_currentPath));
        }
    }

    private void NewFolder()
    {
        if (
            !_fileOperationsHandler.NewDirAt(
                _currentPath,
                "New Directory",
                out string? errorMessage
            )
        )
        {
            _errorMessage = errorMessage;
            return;
        }
        _needRefresh = true;
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
        IUndoableFileOperation? action = _undoRedoHandler.Current;
        action ??= _undoRedoHandler.GetPrevious();
        if (action is null)
            return;

        if (action.Undo(out _errorMessage))
            _undoRedoHandler.GetPrevious();
        else
            _undoRedoHandler.RemoveNode(true);
    }

    private void Redo()
    {
        IUndoableFileOperation? action = _undoRedoHandler.GetNext();
        if (action is null)
            return;

        if (!action.Redo(out _errorMessage))
            _undoRedoHandler.RemoveNode(false);
    }

    private void SelectAll() { }

    private void SelectNone() { }

    private void InvertSelection() { }

    private void Pull() => _needRefresh = _versionControl.DownloadChanges(out _errorMessage);

    private void Commit() { }

    private void Push() { }

    private void ListView() { }

    private void IconView() { }
#pragma warning restore CA1822 // Mark members as static
}
