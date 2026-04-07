using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileInformation;
using FileSurfer.Core.Services.Sftp;
using ReactiveUI;

namespace FileSurfer.Core.ViewModels;

public record SyncEventViewModel
{
    public required string LocalBasePath { get; init; }
    public required string LocalRelPath { get; init; }
    public required string RemoteBasePath { get; init; }
    public required string RemoteRelPath { get; init; }
    public required FileSystemEventType OpType { get; init; }
    public required DateTime TimeStamp { get; init; }
    public string TimeStampStr => TimeStamp.ToLongTimeString();
}

public class SyncEventVmFactory
{
    private readonly string _localRoot;
    private readonly string _remoteRoot;

    public SyncEventVmFactory(string localRoot, string remoteRoot)
    {
        _localRoot = LocalPathTools.NormalizePath(localRoot) + LocalPathTools.DirSeparator;
        _remoteRoot =
            RemoteUnixPathTools.NormalizePath(remoteRoot) + RemoteUnixPathTools.DirSeparator;
    }

    public SyncEventViewModel GetEvent(FileSystemEvent fsEvent, string remotePath) =>
        new()
        {
            LocalBasePath = _localRoot,
            LocalRelPath = MakeRelative(_localRoot, fsEvent.OriginalPath, LocalPathTools.Instance),
            OpType = fsEvent.EventType,
            RemoteBasePath = _remoteRoot,
            RemoteRelPath = MakeRelative(_remoteRoot, remotePath, RemoteUnixPathTools.Instance),
            TimeStamp = DateTime.Now,
        };

    private static string MakeRelative(string basePath, string absolutePath, IPathTools pathTools)
    {
        absolutePath = pathTools.NormalizePath(absolutePath);
        return absolutePath[basePath.Length..];
    }
}

public class SftpSynchronizerViewModel : ReactiveObject, IAsyncDisposable
{
    private const string SyncErrorTitle = "Synchronization Error";

    private readonly LocalToSftpSynchronizer _synchronizer;
    private readonly IDialogService _dialogService;
    private readonly SyncEventVmFactory _syncEventVmFactory;

    public string LocalDirLabel { get; }
    public Location LocalDir { get; }
    public string RemoteDirLabel { get; }
    public Location RemoteDir { get; }

    public bool InitFromRemote
    {
        get => _initFromRemote;
        set => this.RaiseAndSetIfChanged(ref _initFromRemote, value);
    }
    private bool _initFromRemote = false;

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

    public bool Synchronizing
    {
        get => _synchronizing;
        set => this.RaiseAndSetIfChanged(ref _synchronizing, value);
    }
    private bool _synchronizing = false;

    public ObservableCollection<SyncEventViewModel> SyncEvents { get; } = [];

    public ReactiveCommand<Unit, Task> StartSyncCommand { get; }
    public ReactiveCommand<Unit, Task> StopSyncCommand { get; }

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
        _synchronizer.OnSyncEvent += ShowEvent;

        StartSyncCommand = ReactiveCommand.Create(StartSynchronization);
        StopSyncCommand = ReactiveCommand.Create(StopSynchronization);
    }

    private void ShowIfError(IResult result)
    {
        if (!result.IsOk)
            foreach (string error in result.Errors.Where(e => !string.IsNullOrWhiteSpace(e)))
                _dialogService.InfoDialog(SyncErrorTitle, error);
    }

    private async Task ShowEvent(FileSystemEvent fsEvent, string remotePath, IResult result)
    {
        ShowIfError(result);
        SyncEventViewModel e = _syncEventVmFactory.GetEvent(fsEvent, remotePath);
        await Dispatcher.UIThread.InvokeAsync(() => SyncEvents.Add(e));
    }

    private async Task StartSynchronization()
    {
        SyncEvents.Clear();

        Synchronizing = true;

        IResult result = await _dialogService.ProgressDialog(
            "Initial synchronization",
            (r, ct) => _synchronizer.Initialize(InitFromRemote, r, ct)
        );
        if (result.IsOk)
            result = await _synchronizer.StartAsync();

        Synchronizing = false;

        ShowIfError(result);
    }

    private async Task StopSynchronization() => await _synchronizer.StopAsync();

    public static async Task<ValueResult<string>> GetLocalPath(
        Location remoteLocation,
        IEnumerable<Location> pastLocations,
        IDialogService dialogService
    )
    {
        if (!remoteLocation.Exists())
            return ValueResult<string>.Error(
                $"Remote directory \"{remoteLocation.Path}\" does not exist."
            );

        const string title = "Pick local path";
        string context = $"""
            Select local path for 
            remote path: "{remoteLocation.FileSystem.GetLabel()}:{remoteLocation.Path}".
            """;
        const string suggestLabel = "Recent local paths:";
        IEnumerable<string> suggestions = pastLocations
            .Where(l => l.FileSystem is LocalFileSystem)
            .Select(l => l.Path)
            .Distinct();

        string? path = await dialogService.SuggestInputDialog(
            title,
            context,
            suggestLabel,
            suggestions
        );
        return path is not null ? path.OkResult() : ValueResult<string>.Error();
    }

    public async ValueTask DisposeAsync()
    {
        await StopSynchronization();
        await _synchronizer.DisposeAsync();
        await CastAndDispose(StartSyncCommand);
        await CastAndDispose(StopSyncCommand);
        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAs)
                await resourceAs.DisposeAsync();
            else
                resource.Dispose();
        }
    }

    /// <summary>
    /// Do not use, only for design preview to work properly
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    public SftpSynchronizerViewModel() { }
}
