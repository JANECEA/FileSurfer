using System;
using System.Windows.Input;
using ReactiveUI;

namespace FileSurfer.ViewModels;

#pragma warning disable CA1822 // Mark members as static
public class MainWindowViewModel : ViewModelBase
{
    private string[] _selectedFiles = Array.Empty<string>();

    private IVersionControl _versionControl = new GitVersionControlHandler();
    private IFileOperationsHandler _fileOperationsHandler = new WindowsFileOperationsHandler();
    private UndoRedoHandler _undoRedoHandler = new();

    public ICommand GoBackCommand { get; }
    public ICommand GoForwardCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand OpenPowerShellCommand { get; }
    public ICommand ChangePathCommand { get; }
    public ICommand SearchDirectoryCommand { get; }
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
    public ICommand SwitchBranchCommand { get; }
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
        ChangePathCommand = ReactiveCommand.Create(ChangePath);
        SearchDirectoryCommand = ReactiveCommand.Create(SearchDirectory);
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
        SwitchBranchCommand = ReactiveCommand.Create(SwitchBranch);
        PullCommand = ReactiveCommand.Create(Pull);
        CommitCommand = ReactiveCommand.Create(Commit);
        PushCommand = ReactiveCommand.Create(Push);
        ListViewCommand = ReactiveCommand.Create(ListView);
        IconViewCommand = ReactiveCommand.Create(IconView);
    }

    private void GoBack() { throw new ExecutionEngineException(); }
    private void GoForward() {}
    private void Reload() {}
    private void OpenPowerShell() {}
    private void ChangePath() {}
    private void SearchDirectory() {}
    private void NewFile() {}
    private void NewFolder() {}
    private void Cut() {}
    private void Copy() {}
    private void Paste() {}
    private void Rename() {}
    private void MoveToTrash() {}
    private void Delete() {}
    private void SortByName() {}
    private void SortByDate() {}
    private void SortByType() {}
    private void Undo() {}
    private void Redo() {}
    private void SelectAll() {}
    private void SelectNone() {}
    private void InvertSelection() {}
    private void SwitchBranch() {}
    private void Pull() {}
    private void Commit() {}
    private void Push() {}
    private void ListView() {}
    private void IconView() {}
}
#pragma warning restore CA1822 // Mark members as static