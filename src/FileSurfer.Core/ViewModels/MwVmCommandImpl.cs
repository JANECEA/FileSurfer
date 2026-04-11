using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.Sftp;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations.Undoable;
using FileSurfer.Core.Services.VersionControl;
using FileSurfer.Core.Views;

namespace FileSurfer.Core.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void SoftReload() => LockReload(false);

    private void HardReload() => LockReload(true);

    private void LockReload(bool forceHardReload)
    {
        if (Interlocked.CompareExchange(ref _isLoading, 1, 0) == 1)
            return;

        _ = _dialogService.BlockingDialogAsync(
            "Reloading",
            async () =>
            {
                await Reload(forceHardReload);
                Interlocked.Exchange(ref _isLoading, 0);
                return SimpleResult.Ok();
            }
        );
    }

    private async Task Reload(bool forceHardReload)
    {
        if (Searching)
            return;

        IResult result = await UpdateEntries(forceHardReload);
        ShowIfError(result);
        if (!result.IsOk)
            return;

        CheckVersionControl(CurrentLocation);
        CurrentInfoMessage = FileEntries.Count > 0 ? null : EmptyDirMessage;
        _lastRefreshedUtc = DateTime.UtcNow;
    }

    private void ShowError(string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
            _dialogService.InfoDialog("Unexpected error", errorMessage);
    }

    private void ShowInfo(string? infoMessage)
    {
        if (!string.IsNullOrWhiteSpace(infoMessage))
            _dialogService.InfoDialog("Info dialog", infoMessage);
    }

    private void ShowIfError(IResult result)
    {
        if (!result.IsOk)
            foreach (string errorMessage in result.Errors)
                ShowError(errorMessage);
    }

    private async Task OpenEntry(FileSystemEntryViewModel entry)
    {
        if (entry.FileSystemEntry is DirectoryEntry)
            await SetNewLocation(entry.PathToEntry);
        else if (CurrentFs.FileInfoProvider.IsLinkedToDirectory(entry.PathToEntry, out string? dir))
            await SetNewLocation(dir);
        else if (CurrentFs is LocalFileSystem fs)
            ShowIfError(fs.LocalShellHandler.OpenFile(entry.PathToEntry));
    }

    private async Task OpenSideBarEntry(SideBarEntryViewModel entry)
    {
        if (entry.IsDirectory)
            await SetLocation(_localFs.GetLocation(entry.PathToEntry));
        else if (CurrentFs.FileInfoProvider.IsLinkedToDirectory(entry.PathToEntry, out string? dir))
            await SetLocation(_localFs.GetLocation(dir));
        else
            ShowIfError(_localFs.LocalShellHandler.OpenFile(entry.PathToEntry));
    }

    private async Task OpenSftpConnectionAsync(SftpConnectionViewModel connectionVm)
    {
        SftpConnection connection = connectionVm.SftpConnection;

        if (connectionVm.FileSystem is not null)
        {
            string initialDir =
                connection.InitialDirectory ?? connectionVm.FileSystem.FileInfoProvider.GetRoot();
            await SetLocation(new Location(connectionVm.FileSystem, initialDir));
            return;
        }
        ValueResult<SftpFileSystem> result = await SftpFsFactory.TryConnectAsync(connection);
        ShowIfError(result);
        if (!result.IsOk)
            return;

        SftpFileSystem fs = result.Value;
        connectionVm.FileSystem = fs;

        if (string.IsNullOrWhiteSpace(connection.InitialDirectory))
            await SetLocation(new Location(fs, fs.FileInfoProvider.GetRoot()));
        else
            await SetLocation(new Location(fs, connection.InitialDirectory));
    }

    private async Task CloseSftpConnection(SftpConnectionViewModel connectionVm)
    {
        if (connectionVm.FileSystem is null)
        {
            ShowError("Connection is not active.");
            return;
        }

        SftpFileSystem fileSystem = connectionVm.FileSystem;
        fileSystem.Dispose();
        connectionVm.FileSystem = null;
        if (ReferenceEquals(fileSystem, CurrentFs))
            await SetLocation(new Location(_localFs, _localFs.LocalFileInfoProvider.GetRoot()));
    }

    private void OpenAs(FileSystemEntryViewModel entry)
    {
        if (!entry.IsDirectory)
            ShowIfError(CurrentFs.FileProperties.ShowOpenAsDialog(entry.FileSystemEntry));
    }

    private Task OpenEntries()
    {
        FileSystemEntryViewModel[] entries = SelectedFiles.ConvertToArray();
        if (CurrentFs is not LocalFileSystem || entries.Length == 0)
            return Task.CompletedTask;

        foreach (FileSystemEntryViewModel e in entries)
            if (
                e.IsDirectory
                || CurrentFs.FileInfoProvider.IsLinkedToDirectory(e.PathToEntry, out _)
            )
                return Task.CompletedTask;

        foreach (FileSystemEntryViewModel entry in entries)
            ShowIfError(_localFs.LocalShellHandler.OpenFile(entry.PathToEntry));

        return Task.CompletedTask;
    }

    private void OpenInNotepad()
    {
        if (CurrentFs is not LocalFileSystem fs)
            return;

        foreach (FileSystemEntryViewModel entry in SelectedFiles)
            if (!entry.IsDirectory)
                ShowIfError(fs.LocalShellHandler.OpenInNotepad(entry.PathToEntry));
    }

    private Task GoUp()
    {
        IPathTools pathTools = CurrentFs.FileInfoProvider.PathTools;
        string parent = pathTools.GetParentDir(pathTools.NormalizePath(CurrentDir));

        if (!string.IsNullOrWhiteSpace(parent))
            return SetNewLocation(parent);

        return Task.CompletedTask;
    }

    private void AddToQuickAccess(FileSystemEntryViewModel? entry) =>
        QuickAccess.Add(
            entry is not null
                ? new SideBarEntryViewModel(_localFs, entry.FileSystemEntry)
                : new SideBarEntryViewModel(
                    _localFs,
                    new DirectoryEntry(CurrentDir, LocalPathTools.Instance)
                )
        );

    private async Task<IResult> UpdateEntries(bool forceHardReload)
    {
        if (!forceHardReload && !await CompareSetLastWriteTime())
            return SimpleResult.Ok();

        ValueResult<DirectoryContents> contentsR =
            await CurrentFs.FileInfoProvider.GetPathEntriesAsync(
                CurrentLocation.Path,
                FileSurferSettings.ShowHiddenFiles,
                FileSurferSettings.ShowProtectedFiles,
                CancellationToken.None
            );
        if (!contentsR.IsOk)
            return contentsR;

        LoadDirEntries(CurrentLocation, contentsR.Value);
        return SimpleResult.Ok();
    }

    private void LoadDirEntries(Location location, DirectoryContents contents)
    {
        IFileSystem fs = location.FileSystem;

        FileSystemEntryViewModel[] dirs = contents.Dirs.ConvertToArray(
            entry => new FileSystemEntryViewModel(fs, entry)
        );
        FileSystemEntryViewModel[] files = contents.Files.ConvertToArray(
            entry => new FileSystemEntryViewModel(fs, entry)
        );

        AddEntries(dirs, files);
    }

    private void AddEntries(FileSystemEntryViewModel[] dirs, FileSystemEntryViewModel[] files)
    {
        SortBy sortBy = FileSurferSettings.SortingMode;
        bool sortReversed = FileSurferSettings.SortReversed;

        SortInPlace(files, sortBy);
        SortInPlace(dirs, sortBy);

        HashSet<string>? selectedPaths = null;
        if (SelectedFiles.Count > 0)
            selectedPaths = SelectedFiles.Select(entry => entry.Name).ToHashSet();

        FileEntries.Clear();
        if (!sortReversed || sortBy is SortBy.Type or SortBy.Size)
            for (int i = 0; i < dirs.Length; i++)
                FileEntries.Add(dirs[i]);
        else
            for (int i = dirs.Length - 1; i >= 0; i--)
                FileEntries.Add(dirs[i]);

        if (!sortReversed)
            for (int i = 0; i < files.Length; i++)
                FileEntries.Add(files[i]);
        else
            for (int i = files.Length - 1; i >= 0; i--)
                FileEntries.Add(files[i]);

        if (selectedPaths is null)
            return;

        foreach (FileSystemEntryViewModel entry in FileEntries)
            if (selectedPaths.Contains(entry.Name))
                SelectedFiles.Add(entry);
    }

    private static void SortInPlace(FileSystemEntryViewModel[] entries, SortBy sortBy)
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

    private void CheckVersionControl(Location location)
    {
        IsVersionControlled =
            FileSurferSettings.GitIntegration
            && location.FileSystem.GitIntegration.InitIfGitRepository(location.Path);

        if (IsVersionControlled)
        {
            LoadBranches(location);
            LoadRepoStateInfo();
            foreach (FileSystemEntryViewModel e in FileEntries)
                e.UpdateGitStatus(GetGitStatus(e.PathToEntry, location.FileSystem));
        }
    }

    private GitStatus GetGitStatus(string path, IFileSystem fileSystem) =>
        IsVersionControlled
            ? fileSystem.GitIntegration.GetStatus(path)
            : GitStatus.NotVersionControlled;

    private void LoadBranches(Location location)
    {
        string currentBranch = location.FileSystem.GitIntegration.GetCurrentBranchName();
        string[] branches = location.FileSystem.GitIntegration.GetBranches();
        if (CurrentBranch == currentBranch && Branches.EqualsUnordered(branches))
            return;

        Branches.Clear();
        foreach (string branch in branches)
        {
            Branches.Add(branch);

            if (string.Equals(currentBranch, branch, StringComparison.Ordinal))
                currentBranch = branch; // Object references must match
        }
        CurrentBranch = currentBranch;
    }

    private void LoadRepoStateInfo() =>
        RepoStateInfo = CurrentFs.GitIntegration.GetRepositoryState() is RepoDetails info
            ? new RepoStateInfo(info.CommitsToPull.ToString(), info.CommitsToPush.ToString())
            : new RepoStateInfo(NoRemoteMark, NoRemoteMark);

    private async Task<IResult> SetLocationInternal(Location location)
    {
        ValueResult<DirectoryContents> contentsR = await _dialogService.BlockingDialogAsync(
            "Loading...",
            async () =>
            {
                if (!await location.ExistsAsync())
                    return ValueResult<DirectoryContents>.Error(
                        $"Location {location.FileSystem.GetLabel()}:{location.Path} does not exist."
                    );

                if (Searching)
                    await CancelSearch();

                return await location.FileSystem.FileInfoProvider.GetPathEntriesAsync(
                    location.Path,
                    FileSurferSettings.ShowHiddenFiles,
                    FileSurferSettings.ShowProtectedFiles,
                    CancellationToken.None
                );
            }
        );
        if (!contentsR.IsOk)
            return contentsR;

        LoadDirEntries(location, contentsR.Value);
        CheckVersionControl(location);
        CurrentLocation = location;
        _lastRefreshedUtc = DateTime.UtcNow;
        CurrentInfoMessage = FileEntries.Count > 0 ? null : EmptyDirMessage;

        return SimpleResult.Ok();
    }

    private async Task<IResult> SetLocationNoHistory(Location location)
    {
        if (Interlocked.CompareExchange(ref _isLoading, 1, 0) == 1)
            return SimpleResult.Error();

        try
        {
            return await SetLocationInternal(location);
        }
        finally
        {
            Interlocked.Exchange(ref _isLoading, 0);
        }
    }

    private async Task SetLocation(Location location)
    {
        IResult result = await SetLocationNoHistory(location);

        ShowIfError(result);
        if (result.IsOk && !location.IsSame(_locationHistory.Current))
            _locationHistory.AddNewNode(location);
    }

    private Task SetNewLocation(string path)
    {
        Location location = CurrentFs.GetLocation(path);
        return SetLocation(location);
    }

    private async Task GoBack()
    {
        if (Searching)
            await CancelSearch();

        while (_locationHistory.GetPrevious() is Location prevLocation)
        {
            _locationHistory.MoveToPrevious();

            if (await prevLocation.ExistsAsync())
            {
                await SetLocationNoHistory(prevLocation);
                return;
            }
            _locationHistory.RemoveCurrent(true);
        }
    }

    private async Task GoBackToLocation(LocationDisplay locationDisplay)
    {
        if (Searching)
            await CancelSearch();

        Location location = locationDisplay.GetLocation();
        if (!LocationsBack.Any(l => l.Equals(locationDisplay)))
        {
            ShowError($"Could not find location: \"{location}\" in history.");
            return;
        }

        while (_locationHistory.GetPrevious() is Location nextLocation)
        {
            _locationHistory.MoveToPrevious();
            if (ReferenceEquals(location, nextLocation))
                break;
        }
        await SetLocation(location);
    }

    private async Task GoForward()
    {
        if (Searching)
            await CancelSearch();

        while (_locationHistory.GetNext() is Location nextLocation)
        {
            _locationHistory.MoveToNext();

            if (await nextLocation.ExistsAsync())
            {
                await SetLocationNoHistory(nextLocation);
                return;
            }
            _locationHistory.RemoveCurrent(true);
        }
    }

    private async Task GoForwardToLocation(LocationDisplay locationDisplay)
    {
        if (Searching)
            await CancelSearch();

        Location location = locationDisplay.GetLocation();
        if (!LocationsForward.Any(l => l.Equals(locationDisplay)))
        {
            ShowError($"Could not find location: \"{location}\" in history.");
            return;
        }

        while (_locationHistory.GetNext() is Location nextLocation)
        {
            _locationHistory.MoveToNext();
            if (ReferenceEquals(location, nextLocation))
                break;
        }
        await SetLocation(location);
    }

    private void OpenTerminal()
    {
        if (CurrentFs is LocalFileSystem fs && fs.LocalFileInfoProvider.Exists(CurrentDir).AsDir)
            ShowIfError(fs.LocalShellHandler.OpenTerminalAt(CurrentDir));
    }

    private async Task SearchAsync(string searchQuery)
    {
        if (Searching)
        {
            CurrentInfoMessage = null;
            await _searchManager.CancelSearchAsync();
        }
        Searching = true;
        FileEntries.Clear();

        int? foundEntries = await _searchManager.SearchAsync(CurrentFs, searchQuery, CurrentDir);
        if (foundEntries is 0)
            CurrentInfoMessage = EmptySearchMessage;
    }

    private async Task CancelSearch()
    {
        Searching = false;
        await _searchManager.CancelSearchAsync();
    }

    private async Task CancelSearchAndGoBack()
    {
        await CancelSearch();

        if (_locationHistory.Current is Location location)
        {
            IResult result = await SetLocationNoHistory(location);
            if (!result.IsOk)
                await GoBack();
        }
    }

    private async Task NewFileAsync()
    {
        string newFileName = FileNameGenerator.GetAvailableName(
            CurrentFs.FileInfoProvider,
            CurrentDir,
            FileSurferSettings.NewFileName
        );

        NewFileAt op = new(PathTools, CurrentFs.FileIoHandler, CurrentDir, newFileName);
        IResult result = await _dialogService.BackgroundDialogAsync(
            "Creating new file",
            op.InvokeAsync
        );
        if (result.IsOk)
        {
            SoftReload();
            _undoRedoHistory.AddNewNode(op);
            if (FileEntries.FirstOrDefault(e => e.Name == newFileName) is { } entry)
                SelectedFiles.Add(entry);
        }
        ShowIfError(result);
    }

    private async Task NewDirAsync()
    {
        string newDirName = FileNameGenerator.GetAvailableName(
            CurrentFs.FileInfoProvider,
            CurrentDir,
            FileSurferSettings.NewDirectoryName
        );

        NewDirAt op = new(PathTools, CurrentFs.FileIoHandler, CurrentDir, newDirName);
        IResult result = await _dialogService.BackgroundDialogAsync(
            "Creating new directory",
            op.InvokeAsync
        );
        if (result.IsOk)
        {
            SoftReload();
            _undoRedoHistory.AddNewNode(op);
            if (FileEntries.FirstOrDefault(e => e.Name == newDirName) is { } entry)
                SelectedFiles.Add(entry);
        }
        ShowIfError(result);
    }

    private async Task AddToArchiveAsync()
    {
        string cwd = CurrentDir;
        IFileSystemEntry[] selected = SelectedFiles.ConvertToArray(e => e.FileSystemEntry);
        if (!(await CurrentFs.FileInfoProvider.ExistsAsync(cwd)).AsDir || selected.Length == 0)
            return;

        string archName =
            selected.Length == 1 ? SelectedFiles[0].FileSystemEntry.NameWoExtension : ArchiveName;

        IResult result = await _dialogService.BackgroundDialogAsync<IResult>(
            "Archiving files",
            (r, ct) => CurrentFs.ArchiveManager.ArchiveEntriesAsync(selected, cwd, archName, r, ct)
        );

        ShowIfError(result);
        HardReload();
    }

    private async Task ExtractArchiveAsync()
    {
        string cwd = CurrentDir;
        if (!(await CurrentFs.FileInfoProvider.ExistsAsync(cwd)).AsDir)
            return;

        IFileSystemEntry[] selected = SelectedFiles.ConvertToArray(e => e.FileSystemEntry);
        foreach (IFileSystemEntry entry in selected)
            if (!CurrentFs.ArchiveManager.IsArchived(entry.PathToEntry))
            {
                ShowError($"Entry \"{entry.Name}\" is not an archive.");
                return;
            }

        List<Task> tasks = selected
            .Select(async e =>
            {
                IResult result = await _dialogService.BackgroundDialogAsync<IResult>(
                    "Extracting archive",
                    (r, ct) =>
                        CurrentFs.ArchiveManager.ExtractArchiveAsync(e.PathToEntry, cwd, r, ct)
                );
                ShowIfError(result);
            })
            .ToList();

        await Task.WhenAll(tasks);
        HardReload();
    }

    private async Task CopyPathAsync(FileSystemEntryViewModel? entry) =>
        ShowIfError(
            await ClipboardManager.CopyPathToFileAsync(
                entry is null ? CurrentDir : entry.PathToEntry
            )
        );

    private async Task CutAsync()
    {
        if (SelectedFiles.Count <= 0)
            return;

        IResult result = await ClipboardManager.CutAsync(
            SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry),
            CurrentLocation
        );
        ShowIfError(result);
    }

    private async Task CopyAsync()
    {
        if (SelectedFiles.Count <= 0)
            return;

        IResult result = await ClipboardManager.CopyAsync(
            SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry),
            CurrentLocation
        );
        ShowIfError(result);
    }

    private async Task PasteAsync()
    {
        ValueResult<IUndoableFileOperation?> r = await _dialogService.BackgroundDialogAsync(
            "Pasting...",
            (r, ct) => ClipboardManager.PasteAsync(CurrentLocation, r, ct)
        );

        ShowIfError(r);
        if (r is { IsOk: true, Value: IUndoableFileOperation operation })
            _undoRedoHistory.AddNewNode(operation);

        HardReload();
    }

    private void CreateShortcut(FileSystemEntryViewModel entry)
    {
        IResult result = entry.IsDirectory
            ? CurrentFs.ShellHandler.CreateDirectoryLink(entry.PathToEntry)
            : CurrentFs.ShellHandler.CreateFileLink(entry.PathToEntry);

        ShowIfError(result);
        HardReload();
    }

    private void ShowProperties(FileSystemEntryViewModel entry) =>
        ShowIfError(CurrentFs.FileProperties.ShowFileProperties(entry));

    private async Task SynchronizeDirAsync(FileSystemEntryViewModel? entry)
    {
        if (IsSynchronizerOpen)
        {
            ShowError("Another directory is being synchronized.");
            return;
        }
        if (CurrentFs is not SftpFileSystem sftpFs)
        {
            ShowError("Cannot synchronize with a local directory.");
            return;
        }

        Location remoteLocation = entry is null
            ? CurrentLocation
            : sftpFs.GetLocation(entry.PathToEntry);

        ValueResult<string> localPathResult = await SftpSynchronizerViewModel.GetLocalPath(
            remoteLocation,
            _locationHistory.GetCurrentCollectionReversed(),
            _dialogService
        );
        ShowIfError(localPathResult);
        if (!localPathResult.IsOk)
            return;

        SftpSynchronizerWindow syncWindow = new();
        syncWindow.DataContext = new SftpSynchronizerViewModel(
            new AvaloniaDialogService(syncWindow),
            new Location(_localFs, localPathResult.Value),
            remoteLocation
        );

        IsSynchronizerOpen = true;
        syncWindow.Closed += (_, _) => IsSynchronizerOpen = false;
        syncWindow.Show();
    }

    private Task RenameAsync(string newName)
    {
        newName = newName.Trim();

        if (SelectedFiles.Count == 1)
            return RenameOneAsync(newName);
        if (SelectedFiles.Count > 1)
            return RenameMultipleAsync(newName);

        return Task.CompletedTask;
    }

    private async Task RenameOneAsync(string newName)
    {
        FileSystemEntryViewModel entry = SelectedFiles[0];

        RenameOne op = new(PathTools, CurrentFs.FileIoHandler, entry.FileSystemEntry, newName);

        IResult result = await _dialogService.BackgroundDialogAsync(
            $"Renaming {entry.Name}",
            op.InvokeAsync
        );
        if (result.IsOk)
        {
            _undoRedoHistory.AddNewNode(op);
            HardReload();

            FileSystemEntryViewModel? newEntry = FileEntries.FirstOrDefault(e =>
                CurrentFs.FileInfoProvider.PathTools.NamesAreEqual(e.Name, newName)
            );
            if (newEntry is not null)
                SelectedFiles.Add(newEntry);
        }
        ShowIfError(result);
    }

    private async Task RenameMultipleAsync(string namingPattern)
    {
        IFileSystemEntry[] entries = SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry);
        if (
            !FileNameGenerator.CanBeRenamedCollectively(
                entries,
                CurrentFs.FileInfoProvider.PathTools
            )
        )
        {
            ShowError("Selected entries aren't of the same type.");
            return;
        }

        string[] availableNames = FileNameGenerator.GetAvailableNames(
            CurrentFs.FileInfoProvider,
            entries,
            namingPattern
        );
        RenameMultiple op = new(PathTools, CurrentFs.FileIoHandler, entries, availableNames);
        IResult result = await _dialogService.BackgroundDialogAsync(
            "Renaming multiple files",
            op.InvokeAsync
        );
        if (result.IsOk)
            _undoRedoHistory.AddNewNode(op);

        ShowIfError(result);
        HardReload();
    }

    private void StageIfVersionControlled(IEnumerable<FileSystemEntryViewModel> entries)
    {
        if (!IsVersionControlled)
            return;

        Result result = Result.Ok();
        foreach (FileSystemEntryViewModel entry in entries)
            result.MergeResult(CurrentFs.GitIntegration.StagePath(entry.PathToEntry));

        ShowIfError(result);
    }

    private async Task MoveToTrashAsync()
    {
        MoveFilesToTrash op = new(
            CurrentFs.BinInteraction,
            CurrentFs.FileIoHandler,
            SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry)
        );

        IResult result = await _dialogService.BackgroundDialogAsync(
            "Moving to Trash",
            op.InvokeAsync
        );
        if (result.IsOk)
            _undoRedoHistory.AddNewNode(op);

        StageIfVersionControlled(SelectedFiles);
        ShowIfError(result);
        HardReload();
    }

    private async Task FlattenFolderAsync(FileSystemEntryViewModel entry)
    {
        if (!entry.IsDirectory)
        {
            ShowError($"Cannot flatten a file: \"{entry.Name}\".");
            return;
        }

        FlattenFolder op = new(
            CurrentFs.FileIoHandler,
            CurrentFs.FileInfoProvider,
            entry.PathToEntry
        );
        IResult result = await _dialogService.BackgroundDialogAsync(
            $"Flattening \"{entry.Name}\"",
            op.InvokeAsync
        );
        if (result.IsOk)
            _undoRedoHistory.AddNewNode(op);

        ShowIfError(result);
        HardReload();
    }

    private Task Delete()
    {
        foreach (FileSystemEntryViewModel entry in SelectedFiles)
            ShowIfError(
                entry.IsDirectory
                    ? CurrentFs.FileIoHandler.DeleteDir(entry.PathToEntry)
                    : CurrentFs.FileIoHandler.DeleteFile(entry.PathToEntry)
            );

        StageIfVersionControlled(SelectedFiles);
        HardReload();
        return Task.CompletedTask;
    }

    private Task SetSortBy(SortBy sortBy)
    {
        FileSurferSettings.SortReversed =
            FileSurferSettings.SortingMode == sortBy && !FileSurferSettings.SortReversed;

        FileSurferSettings.SortingMode = sortBy;
        HardReload();
        return Task.CompletedTask;
    }

    private async Task UndoAsync()
    {
        if (_undoRedoHistory.IsTail())
            _undoRedoHistory.MoveToPrevious();

        if (_undoRedoHistory.IsHead())
            return;

        IUndoableFileOperation op =
            _undoRedoHistory.Current ?? throw new InvalidOperationException();

        IResult result = await _dialogService.BackgroundDialogAsync(
            "Undoing Operation",
            op.UndoAsync
        );
        if (result.IsOk)
        {
            _undoRedoHistory.MoveToPrevious();
            HardReload();
        }
        else
        {
            if (FileSurferSettings.ShowUndoRedoErrorDialogs)
                ShowIfError(result);

            _undoRedoHistory.RemoveCurrent(true);
        }
    }

    private async Task RedoAsync()
    {
        _undoRedoHistory.MoveToNext();
        if (_undoRedoHistory.Current is null)
            return;

        IUndoableFileOperation op = _undoRedoHistory.Current;
        IResult result = await _dialogService.BackgroundDialogAsync(
            "Redoing Operation",
            op.InvokeAsync
        );
        if (result.IsOk)
            HardReload();
        else
        {
            if (FileSurferSettings.ShowUndoRedoErrorDialogs)
                ShowIfError(result);

            _undoRedoHistory.RemoveCurrent(true);
        }
    }

    private void SelectAll()
    {
        SelectedFiles.Clear();
        foreach (FileSystemEntryViewModel entry in FileEntries)
            SelectedFiles.Add(entry);
    }

    private void SelectNone() => SelectedFiles.Clear();

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

    private void GitStage(FileSystemEntryViewModel? entry)
    {
        if (IsVersionControlled)
            ShowIfError(CurrentFs.GitIntegration.StagePath(entry?.PathToEntry ?? CurrentDir));
    }

    private void GitUnstage(FileSystemEntryViewModel? entry)
    {
        if (IsVersionControlled)
            ShowIfError(CurrentFs.GitIntegration.UnstagePath(entry?.PathToEntry ?? CurrentDir));
    }

    private void GitRestore(FileSystemEntryViewModel? entry)
    {
        if (IsVersionControlled)
        {
            ShowIfError(CurrentFs.GitIntegration.RestorePath(entry?.PathToEntry ?? CurrentDir));
            HardReload();
        }
    }

    private void GitSwitchBranch(string branchName)
    {
        if (string.IsNullOrEmpty(branchName) || !Branches.Contains(branchName))
            return;

        string prevBranch = CurrentBranch;
        CurrentBranch = branchName;

        IResult result = CurrentFs.GitIntegration.SwitchBranches(branchName);
        ShowIfError(result);
        if (result.IsOk)
            HardReload();
        else
            Dispatcher.UIThread.Post(() => CurrentBranch = prevBranch);
    }

    private void GitStash()
    {
        if (IsVersionControlled)
        {
            ShowIfError(CurrentFs.GitIntegration.StashChanges());
            HardReload();
        }
    }

    private void GitStashPop()
    {
        if (IsVersionControlled)
        {
            ShowIfError(CurrentFs.GitIntegration.PopChanges());
            HardReload();
        }
    }

    private async Task GitFetchAsync()
    {
        if (!IsVersionControlled)
            return;

        IResult result = await _dialogService.BackgroundDialogAsync(
            "Fetching changes",
            () => CurrentFs.GitIntegration.FetchChangesAsync()
        );
        ShowIfError(result);
        SoftReload();
    }

    private async Task GitPullAsync()
    {
        if (!IsVersionControlled)
            return;

        ValueResult<string> result = await _dialogService.BackgroundDialogAsync(
            "Pulling changes",
            () => CurrentFs.GitIntegration.PullChangesAsync()
        );
        if (result.IsOk)
            ShowInfo(result.Value);
        else
            ShowIfError(result);

        HardReload();
    }

    private async Task GitCommitAsync(string commitMessage)
    {
        if (!IsVersionControlled)
            return;

        ValueResult<string> result = await _dialogService.BackgroundDialogAsync(
            "Commiting changes",
            () => CurrentFs.GitIntegration.CommitChangesAsync(commitMessage)
        );
        if (result.IsOk)
            ShowInfo(result.Value);
        else
            ShowIfError(result);

        HardReload();
    }

    private async Task GitPushAsync()
    {
        if (!IsVersionControlled)
            return;

        ValueResult<string> result = await _dialogService.BackgroundDialogAsync(
            "Pushing changes",
            () => CurrentFs.GitIntegration.PushChangesAsync()
        );
        if (result.IsOk)
            ShowInfo(result.Value);
        else
            ShowIfError(result);

        SoftReload();
    }
}
