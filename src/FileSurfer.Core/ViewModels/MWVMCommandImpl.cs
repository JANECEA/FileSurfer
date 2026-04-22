using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.Sftp;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.FileOperations.Undoable;
using FileSurfer.Core.Services.VersionControl;
using FileSurfer.Core.Views;

namespace FileSurfer.Core.ViewModels;

public sealed partial class MainWindowViewModel
{
    private sealed class LoadOperation
    {
        public bool IsSoft { get; }
        public Task LoadTask { get; }

        public bool IsCompleted => LoadTask.IsCompleted;

        private LoadOperation(bool isSoft, Task loadTask)
        {
            IsSoft = isSoft;
            LoadTask = loadTask;
        }

        public static LoadOperation Soft(Task task) => new(true, task);

        public static LoadOperation Hard(Task task) => new(false, task);

        public LoadOperation ChainHardOp(Func<Task> taskFunc) =>
            new(
                false,
                LoadTask
                    .ContinueWith(
                        _ => taskFunc(),
                        TaskScheduler.FromCurrentSynchronizationContext()
                    )
                    .Unwrap()
            );
    }

    private LoadOperation _loadOp = LoadOperation.Soft(Task.CompletedTask);

    private void SoftReload()
    {
        if (_loadOp.IsCompleted)
            _loadOp = LoadOperation.Soft(ExecuteReloadAsync(false));
    }

    private void HardReload()
    {
        if (_loadOp.IsCompleted)
            _loadOp = LoadOperation.Hard(ExecuteReloadAsync(true));
        else if (_loadOp.IsSoft)
            _loadOp = _loadOp.ChainHardOp(() => ExecuteReloadAsync(true));
    }

    private async Task WaitForHardReloadAsync()
    {
        HardReload();
        await _loadOp.LoadTask;
    }

    private async Task ExecuteReloadAsync(bool forceHardReload)
    {
        await _dialogService.BlockingDialogAsync(
            "Reloading",
            async () =>
            {
                await ReloadAsync(forceHardReload);
                return SimpleResult.Ok();
            }
        );
    }

    private async Task ReloadAsync(bool forceHardReload)
    {
        if (Searching)
            return;

        IResult result = await UpdateEntriesAsync(forceHardReload);
        ShowIfError(result);
        if (!result.IsOk)
            return;

        await CheckVersionControlAsync(CurrentLocation);
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

    private async Task OpenEntryAsync(FileSystemEntryViewModel entry)
    {
        IFileSystem fs = CurrentLocation.FileSystem;

        if (entry.FileSystemEntry is DirectoryEntry)
            await SetNewLocationAsync(entry.PathToEntry);
        else if (fs.FileInfoProvider.IsLinkedToDirectory(entry.PathToEntry, out string? dir))
            await SetNewLocationAsync(dir);
        else if (fs.IsLocal())
            ShowIfError(_localFs.LocalShellHandler.OpenFile(entry.PathToEntry));
    }

    private async Task OpenSideBarEntryAsync(SideBarEntryViewModel entry)
    {
        if (entry.IsDirectory)
            await SetLocationAsync(_localFs.GetLocation(entry.PathToEntry));
        else if (
            _localFs.LocalFileInfoProvider.IsLinkedToDirectory(entry.PathToEntry, out string? dir)
        )
            await SetLocationAsync(_localFs.GetLocation(dir));
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
            await SetLocationAsync(new Location(connectionVm.FileSystem, initialDir));
            return;
        }
        ValueResult<SftpFileSystem> result = await SftpFsFactory.TryConnectAsync(connection);
        ShowIfError(result);
        if (!result.IsOk)
            return;

        SftpFileSystem fs = result.Value;
        connectionVm.FileSystem = fs;

        if (string.IsNullOrWhiteSpace(connection.InitialDirectory))
            await SetLocationAsync(new Location(fs, fs.FileInfoProvider.GetRoot()));
        else
            await SetLocationAsync(new Location(fs, connection.InitialDirectory));
    }

    private Task CloseSftpConnection(SftpConnectionViewModel connectionVm)
    {
        if (connectionVm.FileSystem is null)
        {
            ShowError("Connection is not active.");
            return Task.CompletedTask;
        }

        SftpFileSystem remoteFs = connectionVm.FileSystem;
        remoteFs.Dispose();
        connectionVm.FileSystem = null;

        if (ReferenceEquals(remoteFs, CurrentLocation.FileSystem))
            return SetLocationAsync(
                new Location(_localFs, _localFs.LocalFileInfoProvider.GetRoot())
            );

        return Task.CompletedTask;
    }

    private void OpenAs(FileSystemEntryViewModel entry)
    {
        if (!entry.IsDirectory)
            ShowIfError(
                CurrentLocation.FileSystem.FileProperties.ShowOpenAsDialog(entry.FileSystemEntry)
            );
    }

    private Task OpenEntries()
    {
        IFileSystem fs = CurrentLocation.FileSystem;
        FileSystemEntryViewModel[] entries = SelectedFiles.ConvertToArray();

        if (!fs.IsLocal() || entries.Length == 0)
            return Task.CompletedTask;

        foreach (FileSystemEntryViewModel e in entries)
            if (e.IsDirectory || fs.FileInfoProvider.IsLinkedToDirectory(e.PathToEntry, out _))
                return Task.CompletedTask;

        foreach (FileSystemEntryViewModel entry in entries)
            ShowIfError(_localFs.LocalShellHandler.OpenFile(entry.PathToEntry));

        return Task.CompletedTask;
    }

    private void OpenInNotepad()
    {
        IFileSystem fs = CurrentLocation.FileSystem;
        if (!fs.IsLocal())
            return;

        foreach (FileSystemEntryViewModel e in SelectedFiles)
            if (!e.IsDirectory)
                ShowIfError(_localFs.LocalShellHandler.OpenInNotepad(e.PathToEntry));
    }

    private Task GoUpAsync()
    {
        Location current = CurrentLocation;
        IPathTools pathTools = current.PathTools();

        string parent = pathTools.GetParentDir(pathTools.NormalizePath(current.Path));

        if (!string.IsNullOrWhiteSpace(parent))
            return SetNewLocationAsync(parent);

        return Task.CompletedTask;
    }

    private void AddToQuickAccess(FileSystemEntryViewModel? entry)
    {
        Location current = CurrentLocation;
        if (!current.FileSystem.IsLocal())
        {
            ShowError("Cannot add remote directories to Quick Access.");
            return;
        }

        IFileSystemEntry fsEntry =
            entry?.FileSystemEntry ?? new DirectoryEntry(current.Path, LocalPathTools.Instance);

        QuickAccess.Add(new SideBarEntryViewModel(_localFs, fsEntry));
    }

    private async Task<IResult> UpdateEntriesAsync(bool forceHardReload)
    {
        Location current = CurrentLocation;

        if (!forceHardReload && !await CompareSetLastWriteTimeAsync(current))
            return SimpleResult.Ok();

        ValueResult<DirectoryContents> contentsR =
            await current.FileSystem.FileInfoProvider.GetPathEntriesAsync(
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

    private async Task CheckVersionControlAsync(Location location)
    {
        IsVersionControlled =
            FileSurferSettings.GitIntegration
            && location.FileSystem.GitIntegration.InitIfGitRepository(location.Path);

        if (IsVersionControlled)
        {
            LoadBranches(location);
            LoadRepoStateInfo(location);
            await SetGitStatusesAsync(location);
        }
    }

    private async Task SetGitStatusesAsync(Location location)
    {
        FileSystemEntryViewModel[] entries = FileEntries.ConvertToArray();
        GitStatus[] statuses = await Task.Run(() =>
            entries.ConvertToArray(e => GetGitStatus(e, location.FileSystem))
        );
        for (int i = 0; i < statuses.Length; i++)
            entries[i].UpdateGitStatus(statuses[i]);
    }

    private GitStatus GetGitStatus(FileSystemEntryViewModel entry, IFileSystem fileSystem) =>
        IsVersionControlled
            ? fileSystem.GitIntegration.GetStatus(entry.PathToEntry)
            : GitStatus.NotVersionControlled;

    private void LoadBranches(Location location)
    {
        string currentBranch = location.FileSystem.GitIntegration.GetCurrentBranchName();
        string[] branches = location.FileSystem.GitIntegration.GetBranches();

        if (
            string.Equals(CurrentBranch, currentBranch, StringComparison.Ordinal)
            && Branches.EqualsUnordered(branches)
        )
            return;

        Branches.Clear();
        foreach (string branch in branches)
        {
            Branches.Add(branch);

            if (string.Equals(currentBranch, branch, StringComparison.Ordinal)) // References must match
                currentBranch = branch;
        }

        SetCurrentBranchSilent(currentBranch);
    }

    private void LoadRepoStateInfo(Location location) =>
        RepoStateInfo = location.FileSystem.GitIntegration.GetRepositoryState() is RepoDetails info
            ? new RepoStateInfo(
                info.CommitsToPull.ToString(NumberFormatInfo.InvariantInfo),
                info.CommitsToPush.ToString(NumberFormatInfo.InvariantInfo)
            )
            : new RepoStateInfo(NoRemoteMark, NoRemoteMark);

    private async Task<ValueResult<DirectoryContents>> GetEntriesAsync(Location location)
    {
        if (!await location.ExistsAsync())
            return ValueResult<DirectoryContents>.Error(
                $"Location {location.FileSystem.GetLabel()}:{location.Path} does not exist."
            );

        return await location.FileSystem.FileInfoProvider.GetPathEntriesAsync(
            location.Path,
            FileSurferSettings.ShowHiddenFiles,
            FileSurferSettings.ShowProtectedFiles,
            CancellationToken.None
        );
    }

    private async Task<IResult> SetLocationInternalAsync(Location location)
    {
        string dirName = location.PathTools().GetFileName(location.Path);

        ValueResult<DirectoryContents> contentsR = await _dialogService.BlockingDialogAsync(
            $"Loading contents of \"{location.FileSystem.GetLabel()} : {dirName}\"",
            () => GetEntriesAsync(location)
        );
        if (!contentsR.IsOk)
            return contentsR;

        if (Searching)
            await CancelSearchAsync();

        LoadDirEntries(location, contentsR.Value);
        await CheckVersionControlAsync(location);
        CurrentLocation = location;
        _lastRefreshedUtc = DateTime.UtcNow;
        CurrentInfoMessage = FileEntries.Count > 0 ? null : EmptyDirMessage;

        return SimpleResult.Ok();
    }

    private Task<IResult> SetLocationNoHistoryAsync(Location location)
    {
        Task<IResult> resultTask = _loadOp
            .LoadTask.ContinueWith(
                _ => SetLocationInternalAsync(location),
                TaskScheduler.FromCurrentSynchronizationContext()
            )
            .Unwrap();
        _loadOp = LoadOperation.Hard(resultTask);
        return resultTask;
    }

    private async Task SetLocationAsync(Location location)
    {
        IResult result = await SetLocationNoHistoryAsync(location);

        ShowIfError(result);
        if (result.IsOk && !location.IsSame(_locationHistory.Current))
            _locationHistory.AddNewNode(location);
    }

    private Task SetNewLocationAsync(string path)
    {
        Location location = CurrentLocation.FileSystem.GetLocation(path);
        return SetLocationAsync(location);
    }

    private async Task GoBackAsync()
    {
        if (Searching)
            await CancelSearchAsync();

        while (_locationHistory.GetPrevious() is Location prevLocation)
        {
            _locationHistory.MoveToPrevious();

            if (await prevLocation.ExistsAsync())
            {
                await SetLocationNoHistoryAsync(prevLocation);
                return;
            }
            _locationHistory.RemoveCurrent(true);
        }
    }

    private async Task GoBackToLocationAsync(LocationDisplay locationDisplay)
    {
        if (Searching)
            await CancelSearchAsync();

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
        await SetLocationAsync(location);
    }

    private async Task GoForwardAsync()
    {
        if (Searching)
            await CancelSearchAsync();

        while (_locationHistory.GetNext() is Location nextLocation)
        {
            _locationHistory.MoveToNext();

            if (await nextLocation.ExistsAsync())
            {
                await SetLocationNoHistoryAsync(nextLocation);
                return;
            }
            _locationHistory.RemoveCurrent(true);
        }
    }

    private async Task GoForwardToLocationAsync(LocationDisplay locationDisplay)
    {
        if (Searching)
            await CancelSearchAsync();

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
        await SetLocationAsync(location);
    }

    private void OpenTerminal()
    {
        Location current = CurrentLocation;

        if (
            current.FileSystem.IsLocal()
            && _localFs.LocalFileInfoProvider.Exists(current.Path).AsDir
        )
            ShowIfError(_localFs.LocalShellHandler.OpenTerminalAt(current.Path));
    }

    private async Task SearchAsync(string searchQuery)
    {
        IFileSystem fs = CurrentLocation.FileSystem;
        string currentDir = CurrentLocation.Path;

        CurrentQuery = searchQuery;
        if (Searching)
        {
            CurrentInfoMessage = null;
            await _searchManager.CancelSearchAsync();
        }
        Searching = true;
        FileEntries.Clear();

        int? foundEntries = await _searchManager.SearchAsync(fs, searchQuery, currentDir);
        if (foundEntries is 0)
            CurrentInfoMessage = EmptySearchMessage;
    }

    private async Task CancelSearchAsync()
    {
        Searching = false;
        CurrentQuery = string.Empty;
        await _searchManager.CancelSearchAsync();
    }

    private async Task CancelSearchAndGoBackAsync()
    {
        await CancelSearchAsync();

        if (_locationHistory.Current is Location location)
        {
            IResult result = await SetLocationNoHistoryAsync(location);
            if (!result.IsOk)
                await GoBackAsync();
        }
    }

    private async Task NewFileAsync()
    {
        Location current = CurrentLocation;
        IFileSystem fs = current.FileSystem;
        IPathTools pathTools = current.PathTools();

        string newFileName = null!;
        NewFileAt op = null!;
        IResult result = await _dialogService.BackgroundDialogAsync(
            "Creating a new file",
            async (r, ct) =>
            {
                newFileName = await FileNameGenerator.GetAvailableNameAsync(
                    fs.FileInfoProvider,
                    current.Path,
                    FileSurferSettings.NewFileName
                );
                op = new NewFileAt(pathTools, fs.FileIoHandler, current.Path, newFileName);
                return await op.InvokeAsync(r, ct);
            }
        );

        if (result.IsOk)
        {
            await WaitForHardReloadAsync();
            _undoRedoHistory.AddNewNode(op);
            if (FileEntries.FirstOrDefault(e => e.Name == newFileName) is { } entry)
                SelectedFiles.Add(entry);
        }
        ShowIfError(result);
    }

    private async Task NewDirAsync()
    {
        Location current = CurrentLocation;
        IFileSystem fs = current.FileSystem;
        IPathTools pathTools = current.PathTools();

        string newDirName = null!;
        NewDirAt op = null!;
        IResult result = await _dialogService.BackgroundDialogAsync(
            "Creating a new directory",
            async (r, ct) =>
            {
                newDirName = await FileNameGenerator.GetAvailableNameAsync(
                    fs.FileInfoProvider,
                    current.Path,
                    FileSurferSettings.NewDirectoryName
                );
                op = new NewDirAt(pathTools, fs.FileIoHandler, current.Path, newDirName);
                return await op.InvokeAsync(r, ct);
            }
        );

        if (result.IsOk)
        {
            await WaitForHardReloadAsync();
            _undoRedoHistory.AddNewNode(op);
            if (FileEntries.FirstOrDefault(e => e.Name == newDirName) is { } entry)
                SelectedFiles.Add(entry);
        }
        ShowIfError(result);
    }

    private async Task AddToArchiveAsync()
    {
        Location current = CurrentLocation;
        IFileSystem fs = current.FileSystem;
        string cd = current.Path;

        IFileSystemEntry[] selected = SelectedFiles.ConvertToArray(e => e.FileSystemEntry);
        if (
            !fs.IsLocal()
            || !(await fs.FileInfoProvider.ExistsAsync(cd)).AsDir
            || selected.Length == 0
        )
            return;

        string archName =
            selected.Length == 1 ? SelectedFiles[0].FileSystemEntry.NameWoExtension : ArchiveName;

        IResult result = await _dialogService.BackgroundDialogAsync<IResult>(
            "Archiving files",
            (r, ct) => fs.ArchiveManager.ArchiveEntriesAsync(selected, cd, archName, r, ct)
        );

        ShowIfError(result);
        HardReload();
    }

    private async Task ExtractArchiveAsync()
    {
        Location current = CurrentLocation;
        IFileSystem fs = current.FileSystem;
        string cd = current.Path;

        if (!fs.IsLocal() || !(await fs.FileInfoProvider.ExistsAsync(cd)).AsDir)
            return;

        IFileSystemEntry[] selected = SelectedFiles.ConvertToArray(e => e.FileSystemEntry);
        foreach (IFileSystemEntry entry in selected)
            if (!fs.ArchiveManager.IsArchived(entry.PathToEntry))
            {
                ShowError($"Entry \"{entry.Name}\" is not an archive.");
                return;
            }

        List<Task<IResult>> tasks = selected
            .Select(e =>
                _dialogService.BackgroundDialogAsync<IResult>(
                    "Extracting archive",
                    (r, ct) => fs.ArchiveManager.ExtractArchiveAsync(e.PathToEntry, cd, r, ct)
                )
            )
            .ToList();

        foreach (Task<IResult> task in tasks)
            ShowIfError(await task);

        HardReload();
    }

    private async Task CopyPathAsync(FileSystemEntryViewModel? entry) =>
        ShowIfError(
            await ClipboardManager.CopyPathToFileAsync(
                entry is null ? CurrentLocation.Path : entry.PathToEntry
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
        IFileSystem fs = CurrentLocation.FileSystem;

        IResult result = entry.IsDirectory
            ? fs.ShellHandler.CreateDirectoryLink(entry.PathToEntry)
            : fs.ShellHandler.CreateFileLink(entry.PathToEntry);

        ShowIfError(result);
        HardReload();
    }

    private void ShowProperties(FileSystemEntryViewModel entry) =>
        ShowIfError(CurrentLocation.FileSystem.FileProperties.ShowFileProperties(entry));

    private async Task SynchronizeDirAsync(FileSystemEntryViewModel? entry)
    {
        Location current = CurrentLocation;
        if (current.FileSystem is not SftpFileSystem sftpFs)
        {
            ShowError("Cannot synchronize with a local directory.");
            return;
        }

        Location remoteLocation = entry is null ? current : sftpFs.GetLocation(entry.PathToEntry);

        ValueResult<string> localPathResult = await SftpSynchronizerViewModel.GetLocalPath(
            remoteLocation,
            _localPathsOrdered,
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
        Location current = CurrentLocation;
        IFileSystem fs = current.FileSystem;
        IPathTools pathTools = current.PathTools();

        FileSystemEntryViewModel? entry = SelectedFiles.FirstOrDefault();
        if (entry is null || string.Equals(newName, entry.Name, StringComparison.Ordinal))
            return;

        RenameOne op = new(pathTools, fs.FileIoHandler, entry.FileSystemEntry, newName);

        IResult result = await _dialogService.BackgroundDialogAsync(
            $"Renaming {entry.Name}",
            op.InvokeAsync
        );
        if (result.IsOk)
        {
            _undoRedoHistory.AddNewNode(op);
            StageSilently([entry.FileSystemEntry]);
            await WaitForHardReloadAsync();

            FileSystemEntryViewModel? newEntry = FileEntries.FirstOrDefault(e =>
                pathTools.NamesAreEqual(e.Name, newName)
            );
            if (newEntry is not null)
                SelectedFiles.Add(newEntry);
        }
        ShowIfError(result);
    }

    private async Task RenameMultipleAsync(string namingPattern)
    {
        Location current = CurrentLocation;
        IFileSystem fs = current.FileSystem;
        IPathTools pathTools = current.PathTools();

        IFileSystemEntry[] entries = SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry);
        int dirCount = entries.OfType<DirectoryEntry>().Count();
        if (dirCount != 0 && dirCount != entries.Length)
        {
            ShowError("Selected entries are not of the same type.");
            return;
        }

        string[] availableNames = FileNameGenerator.GetAvailableNames(
            fs.FileInfoProvider,
            entries,
            namingPattern
        );
        RenameMultiple op = new(pathTools, fs.FileIoHandler, entries, availableNames);
        IResult result = await _dialogService.BackgroundDialogAsync(
            "Renaming multiple files",
            op.InvokeAsync
        );
        if (result.IsOk)
        {
            _undoRedoHistory.AddNewNode(op);
            StageSilently(entries);
        }

        ShowIfError(result);
        HardReload();
    }

    private void StageSilently(IEnumerable<IFileSystemEntry> entries)
    {
        if (!IsVersionControlled)
            return;

        IFileSystem fs = CurrentLocation.FileSystem;
        foreach (IFileSystemEntry entry in entries)
            _ = fs.GitIntegration.StagePath(entry.PathToEntry);
    }

    private async Task MoveToTrashAsync()
    {
        IFileSystem fs = CurrentLocation.FileSystem;

        IFileSystemEntry[] entries = SelectedFiles.ConvertToArray(entry => entry.FileSystemEntry);
        MoveFilesToTrash op = new(fs.BinInteraction, fs.FileIoHandler, entries);

        IResult result = await _dialogService.BackgroundDialogAsync(
            "Moving to Trash",
            op.InvokeAsync
        );
        if (result.IsOk)
            _undoRedoHistory.AddNewNode(op);

        StageSilently(entries);
        ShowIfError(result);
        HardReload();
    }

    private async Task FlattenFolderAsync(FileSystemEntryViewModel entry)
    {
        IFileSystem fs = CurrentLocation.FileSystem;

        if (!entry.IsDirectory)
        {
            ShowError($"Cannot flatten a file: \"{entry.Name}\".");
            return;
        }

        FlattenFolder op = new(fs.FileIoHandler, fs.FileInfoProvider, entry.PathToEntry);
        IResult result = await _dialogService.BackgroundDialogAsync(
            $"Flattening \"{entry.Name}\"",
            op.InvokeAsync
        );
        if (result.IsOk)
            _undoRedoHistory.AddNewNode(op);

        ShowIfError(result);
        HardReload();
    }

    private async Task Delete()
    {
        IFileSystem fs = CurrentLocation.FileSystem;
        IFileSystemEntry[] entries = SelectedFiles.ConvertToArray(e => e.FileSystemEntry);

        DeleteFiles op = new(fs.FileIoHandler, entries);
        IResult result = await _dialogService.BackgroundDialogAsync(
            "Deleting files",
            op.InvokeAsync
        );

        ShowIfError(result);
        if (result.IsOk)
            StageSilently(entries);

        HardReload();
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

        IUndoableFileOperation op = _undoRedoHistory.Current ?? throw new UnreachableException();

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
        Location current = CurrentLocation;
        IFileSystem fs = current.FileSystem;

        if (IsVersionControlled)
            ShowIfError(fs.GitIntegration.StagePath(entry?.PathToEntry ?? current.Path));
    }

    private void GitUnstage(FileSystemEntryViewModel? entry)
    {
        Location current = CurrentLocation;
        IFileSystem fs = current.FileSystem;

        if (IsVersionControlled)
            ShowIfError(fs.GitIntegration.UnstagePath(entry?.PathToEntry ?? current.Path));
    }

    private void GitRestore(FileSystemEntryViewModel? entry)
    {
        Location current = CurrentLocation;
        IFileSystem fs = current.FileSystem;

        if (IsVersionControlled)
        {
            ShowIfError(fs.GitIntegration.RestorePath(entry?.PathToEntry ?? current.Path));
            HardReload();
        }
    }

    private void GitSwitchBranch(string branchName)
    {
        if (string.IsNullOrEmpty(branchName) || !Branches.Contains(branchName))
            return;

        string previous = CurrentBranch;
        IResult result = CurrentLocation.FileSystem.GitIntegration.SwitchBranches(branchName);
        ShowIfError(result);
        if (result.IsOk)
            HardReload();
        else
            SetCurrentBranchSilent(previous);
    }

    private void GitStash()
    {
        IFileSystem fs = CurrentLocation.FileSystem;

        if (IsVersionControlled)
        {
            ShowIfError(fs.GitIntegration.StashChanges());
            HardReload();
        }
    }

    private void GitStashPop()
    {
        IFileSystem fs = CurrentLocation.FileSystem;

        if (IsVersionControlled)
        {
            ShowIfError(fs.GitIntegration.PopChanges());
            HardReload();
        }
    }

    private async Task GitFetchAsync()
    {
        IFileSystem fs = CurrentLocation.FileSystem;

        if (!IsVersionControlled)
            return;

        IResult result = await _dialogService.BackgroundDialogAsync(
            "Fetching changes",
            () => fs.GitIntegration.FetchChangesAsync()
        );
        ShowIfError(result);
        SoftReload();
    }

    private async Task GitPullAsync()
    {
        IFileSystem fs = CurrentLocation.FileSystem;

        if (!IsVersionControlled)
            return;

        ValueResult<string> result = await _dialogService.BackgroundDialogAsync(
            "Pulling changes",
            () => fs.GitIntegration.PullChangesAsync()
        );
        if (result.IsOk)
            ShowInfo(result.Value);
        else
            ShowIfError(result);

        HardReload();
    }

    private async Task GitCommitAsync(string commitMessage)
    {
        commitMessage = commitMessage.Trim();
        IFileSystem fs = CurrentLocation.FileSystem;

        if (!IsVersionControlled)
            return;

        ValueResult<string> result = await _dialogService.BackgroundDialogAsync(
            "Commiting changes",
            () => fs.GitIntegration.CommitChangesAsync(commitMessage)
        );
        if (result.IsOk)
            ShowInfo(result.Value);
        else
            ShowIfError(result);

        HardReload();
    }

    private async Task GitPushAsync()
    {
        IFileSystem fs = CurrentLocation.FileSystem;

        if (!IsVersionControlled)
            return;

        ValueResult<string> result = await _dialogService.BackgroundDialogAsync(
            "Pushing changes",
            () => fs.GitIntegration.PushChangesAsync()
        );
        if (result.IsOk)
            ShowInfo(result.Value);
        else
            ShowIfError(result);

        SoftReload();
    }
}
