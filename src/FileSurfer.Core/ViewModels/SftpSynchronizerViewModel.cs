using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileInformation;
using FileSurfer.Core.Services.Sftp;
using ReactiveUI;

namespace FileSurfer.Core.ViewModels;

/// <summary>
/// Represents a single synchronization event shown in the SFTP synchronizer UI.
/// </summary>
public record SyncEventViewModel
{
    /// <summary>
    /// Gets the full local path involved in the synchronization event.
    /// </summary>
    public required string LocalPath { get; init; }

    /// <summary>
    /// Gets the local path relative to the configured local sync root.
    /// </summary>
    public required string LocalRelPath { get; init; }

    /// <summary>
    /// Gets the full remote path involved in the synchronization event.
    /// </summary>
    public required string RemotePath { get; init; }

    /// <summary>
    /// Gets the remote path relative to the configured remote sync root.
    /// </summary>
    public required string RemoteRelPath { get; init; }

    /// <summary>
    /// Gets the file-system operation type for this event.
    /// </summary>
    public required FileSystemEventType OpType { get; init; }

    /// <summary>
    /// Gets the timestamp when the event was recorded.
    /// </summary>
    public required DateTime TimeStamp { get; init; }

    /// <summary>
    /// Gets the timestamp formatted for display.
    /// </summary>
    public string TimeStampStr => TimeStamp.ToLongTimeString();
}

/// <summary>
/// Creates <see cref="SyncEventViewModel"/> instances from synchronization events.
/// </summary>
public class SyncEventVmFactory
{
    private readonly string _localRoot;
    private readonly string _remoteRoot;

    /// <summary>
    /// Initializes the factory with local and remote synchronization roots.
    /// </summary>
    public SyncEventVmFactory(string localRoot, string remoteRoot)
    {
        _localRoot = LocalPathTools.NormalizePath(localRoot) + LocalPathTools.DirSeparator;
        _remoteRoot =
            RemoteUnixPathTools.NormalizePath(remoteRoot) + RemoteUnixPathTools.DirSeparator;
    }

    /// <summary>
    /// Creates a display model for a synchronization event and its mapped remote path.
    /// </summary>
    public SyncEventViewModel GetEvent(FileSystemEvent fsEvent, string remotePath) =>
        new()
        {
            LocalPath = fsEvent.OriginalPath,
            LocalRelPath = MakeRelative(_localRoot, fsEvent.OriginalPath, LocalPathTools.Instance),
            OpType = fsEvent.EventType,
            RemotePath = remotePath,
            RemoteRelPath = MakeRelative(_remoteRoot, remotePath, RemoteUnixPathTools.Instance),
            TimeStamp = DateTime.Now,
        };

    private static string MakeRelative(string basePath, string absolutePath, IPathTools pathTools)
    {
        absolutePath = pathTools.NormalizePath(absolutePath);
        return absolutePath[basePath.Length..];
    }
}

/// <summary>
/// Coordinates initialization and continuous synchronization between local and SFTP locations.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class SftpSynchronizerViewModel : ReactiveObject, IAsyncDisposable
{
    private const string SyncErrorTitle = "Synchronization Error";

    private readonly LocalToSftpSynchronizer _synchronizer;
    private readonly IDialogService _dialogService;
    private readonly SyncEventVmFactory _syncEventVmFactory;
    private CancellationTokenSource? _syncCts;

    /// <summary>
    /// Gets the label shown for the local synchronization location.
    /// </summary>
    public string LocalDirLabel { get; }

    /// <summary>
    /// Gets the local synchronization location.
    /// </summary>
    public Location LocalDir { get; }

    /// <summary>
    /// Gets the label shown for the remote synchronization location.
    /// </summary>
    public string RemoteDirLabel { get; }

    /// <summary>
    /// Gets the remote synchronization location.
    /// </summary>
    public Location RemoteDir { get; }

    /// <summary>
    /// Gets or sets whether initialization should copy from remote to local.
    /// </summary>
    public bool InitFromRemote
    {
        get => _initFromRemote;
        set => this.RaiseAndSetIfChanged(ref _initFromRemote, value);
    }
    private bool _initFromRemote = false;

    /// <summary>
    /// Gets or sets whether hidden files are included in synchronization.
    /// </summary>
    public bool SyncHiddenFiles
    {
        get => _syncHiddenFiles;
        set
        {
            this.RaiseAndSetIfChanged(ref _syncHiddenFiles, value);
            FileSurferSettings.SyncHiddenFiles = value;
        }
    }
    private bool _syncHiddenFiles;

    /// <summary>
    /// Gets or sets whether continuous synchronization is currently running.
    /// </summary>
    public bool Synchronizing
    {
        get => _synchronizing;
        set => this.RaiseAndSetIfChanged(ref _synchronizing, value);
    }
    private bool _synchronizing = false;

    /// <summary>
    /// Gets or sets whether initial synchronization is currently running.
    /// </summary>
    public bool Initializing
    {
        get => _initializing;
        set => this.RaiseAndSetIfChanged(ref _initializing, value);
    }
    private bool _initializing = false;

    /// <summary>
    /// Gets the event list displayed in the synchronization window.
    /// </summary>
    public ObservableCollection<SyncEventViewModel> SyncEvents { get; } = [];

    /// <summary>
    /// Gets the command that starts initialization and synchronization.
    /// </summary>
    public ReactiveCommand<Unit, Task> StartSyncCommand { get; }

    /// <summary>
    /// Gets the command that requests synchronization cancellation.
    /// </summary>
    public ReactiveCommand<Unit, Unit> StopSyncCommand { get; }

    /// <summary>
    /// Creates a synchronizer view model for the given local and remote locations.
    /// </summary>
    public SftpSynchronizerViewModel(
        IDialogService dialogService,
        Location localDir,
        Location remoteDir
    )
    {
        _syncEventVmFactory = new SyncEventVmFactory(localDir.Path, remoteDir.Path);
        _dialogService = dialogService;
        LocalDir = localDir;
        LocalDirLabel = localDir.FileSystem.GetLabel();
        RemoteDir = remoteDir;
        RemoteDirLabel = remoteDir.FileSystem.GetLabel();

        SyncHiddenFiles = FileSurferSettings.SyncHiddenFiles;

        _synchronizer = new LocalToSftpSynchronizer(
            localDir,
            remoteDir,
            new DirectoryWatcher(localDir)
        );
        _synchronizer.OnSyncEvent += ShowEventAsync;

        StartSyncCommand = ReactiveCommand.Create(StartSyncAsync);
        StopSyncCommand = ReactiveCommand.Create(StopSync);
    }

    private void ShowIfError(IResult result)
    {
        if (!result.IsOk)
            foreach (string error in result.Errors.Where(e => !string.IsNullOrWhiteSpace(e)))
                _dialogService.InfoDialog(SyncErrorTitle, error);
    }

    private Task ShowEventAsync(FileSystemEvent fsEvent, string remotePath, IResult result)
    {
        if (!result.IsOk)
        {
            if (_syncCts is not null && !_syncCts.IsCancellationRequested)
                ShowIfError(result);

            _syncCts?.Cancel();
            return Task.CompletedTask;
        }

        SyncEventViewModel e = _syncEventVmFactory.GetEvent(fsEvent, remotePath);
        return Dispatcher.UIThread.InvokeAsync(() => SyncEvents.Add(e)).GetTask();
    }

    private async Task StartSyncAsync()
    {
        if (_syncCts is not null)
            return;

        SyncEvents.Clear();

        Synchronizing = true;
        Initializing = true;

        IResult result = await _dialogService.BlockingDialogAsync(
            "Initial synchronization",
            (r, ct) => _synchronizer.Initialize(InitFromRemote, r, ct)
        );

        Initializing = false;

        if (result.IsOk)
        {
            CancellationTokenSource cts = new();
            _syncCts = cts;

            result = await _synchronizer.SynchronizeAsync(cts.Token);

            _syncCts = null;
            cts.Dispose();
        }

        Synchronizing = false;

        ShowIfError(result);
    }

    private void StopSync()
    {
        CancellationTokenSource? cts = _syncCts;
        if (cts is null)
            return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Synchronization has already completed.
        }
    }

    /// <summary>
    /// Prompts for and validates the local path to pair with a remote synchronization target.
    /// </summary>
    public static async Task<ValueResult<string>> GetLocalPath(
        Location remoteLocation,
        IList<string> pastLocalPaths,
        IDialogService dialogService
    )
    {
        IEnumerable<string> copy = pastLocalPaths.ToList();

        if (!await remoteLocation.ExistsAsync())
            return ValueResult<string>.Error(
                $"Remote directory \"{remoteLocation.Path}\" does not exist."
            );

        const string title = "Pick local path";
        string context = $"""
            Select local path for 
            remote path: "{remoteLocation.FileSystem.GetLabel()}:{remoteLocation.Path}".
            """;
        const string suggestLabel = "Recent local paths:";

        string? path = await dialogService.SuggestInputDialogAsync(
            title,
            context,
            suggestLabel,
            copy.Reverse()
        );
        return path is not null ? path.OkResult() : ValueResult<string>.Error();
    }

    public async ValueTask DisposeAsync()
    {
        StopSync();
        _synchronizer.Dispose();
        await CastAndDispose(StartSyncCommand);
        await CastAndDispose(StopSyncCommand);
        return;

        static ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAs)
                return resourceAs.DisposeAsync();

            resource.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Do not use, only for design preview to work properly
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    public SftpSynchronizerViewModel() { }
}
