using Avalonia.Threading;
using FileSurfer.Views;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FileSurfer.ViewModels;

#pragma warning disable CA1822 // Mark members as static
public class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
{
    private readonly IFileOperationsHandler _fileOperationsHandler = new WindowsFileOperationsHandler();
    private readonly UndoRedoHandler<IUndoableFileOperation> _undoRedoHistory = new();
    private readonly UndoRedoHandler<string> _pathHistory = new();
    private readonly IVersionControl _versionControl;

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

    public ICommand GoBackCommand { get; }
    public ICommand GoForwardCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand OpenPowerShellCommand { get; }
    public ICommand CancelSearchCommand { get; }
    public ICommand NewFileCommand { get; }
    public ICommand NewDirCommand { get; }
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
        _versionControl = new GitVersionControlHandler(_fileOperationsHandler);
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

    private void GoBack() { }

    private void GoForward() { }

    private void Reload() { }

    private void OpenPowerShell()
    {
        _fileOperationsHandler.OpenCmdAt(_currentPath, out string? errorMessage);
        ErrorMessage = errorMessage;
    }

    private void SearchDirectory(string searchQuery) { }

    private void CancelSearch() { }

    private void NewFile()
    {
        string newFileName = _fileOperationsHandler.GetAvailableName(_currentPath, "New File");
        if (_fileOperationsHandler.NewFileAt(_currentPath, newFileName, out string? errorMessage))
        {
            _needRefresh = true;
            _undoRedoHistory.NewNode(new NewFileAt(_fileOperationsHandler, _currentPath, newFileName));
        }
        else
            ErrorMessage = errorMessage;
    }

    private void NewDir()
    {
        string newDirName = _fileOperationsHandler.GetAvailableName(_currentPath, "New Folder");
        if (_fileOperationsHandler.NewDirAt(_currentPath, newDirName, out string? errorMessage))
        {
            _needRefresh = true;
            _undoRedoHistory.NewNode(new NewDirAt(_fileOperationsHandler , _currentPath, newDirName));
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
            _undoRedoHistory.MoveToPrevious();
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
        if (!operation.Redo(out string? errorMessage))
        {
            _undoRedoHistory.RemoveNode(true);
            ErrorMessage= errorMessage;
        }
    }

    private void SelectAll() { }

    private void SelectNone() { }

    private void InvertSelection() { }

    private void Pull() 
    {
        if (_versionControl.DownloadChanges(out string? errorMessage))
        {
            NeedRefresh = true;
        }
        else
            ErrorMessage = errorMessage;
    }

    private void Commit() { }

    private void Push() { }

    private void ListView() { }

    private void IconView() { }
}
#pragma warning restore CA1822 // Mark members as static
