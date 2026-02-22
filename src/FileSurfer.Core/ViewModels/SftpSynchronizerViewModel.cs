using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileInformation;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.Sftp;
using ReactiveUI;

namespace FileSurfer.Core.ViewModels;

public record SynchronizationEvent
{
    public required string LocalPath { get; init; }
    public required string RemotePath { get; init; }
    public required FileSystemEventType OpType { get; init; }
    public required DateTime TimeStamp { get; init; }
    public string TimeStampStr => TimeStamp.ToLongTimeString();
}

public class SftpSynchronizerViewModel : ReactiveObject, IAsyncDisposable
{
    private const string SyncErrorTitle = "Synchronization Error";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly LocalToSftpSynchronizer _synchronizer;
    private readonly IDialogService _dialogService;

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

    public ObservableCollection<SynchronizationEvent> SyncEvents { get; } = [];

    public ReactiveCommand<Unit, Task> StartSyncCommand { get; }
    public ReactiveCommand<Unit, Task> StopSyncCommand { get; }

    public SftpSynchronizerViewModel(
        IDialogService dialogService,
        Location localDir,
        Location remoteDir
    )
    {
        LocalDir = localDir;
        LocalDirLabel = localDir.FileSystem.GetLabel();
        RemoteDir = remoteDir;
        RemoteDirLabel = remoteDir.FileSystem.GetLabel();
        _dialogService = dialogService;

        SyncHiddenFiles = FileSurferSettings.SyncHiddenFiles;

        IRemoteFileIoHandler ioHandler = (IRemoteFileIoHandler)remoteDir.FileSystem.FileIoHandler;
        _synchronizer = new LocalToSftpSynchronizer(remoteDir, localDir, Interval, ioHandler);
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

    private void ShowEvent(FileSystemEvent fsEvent, string remotePath, IResult result)
    {
        ShowIfError(result);
        SynchronizationEvent e = new()
        {
            LocalPath = fsEvent.OriginalPath,
            OpType = fsEvent.EventType,
            RemotePath = remotePath,
            TimeStamp = DateTime.Now,
        };
        SyncEvents.Add(e);
    }

    private async Task StartSynchronization()
    {
        SyncEvents.Clear();

        Synchronizing = true;
        IResult result = await _synchronizer.StartAsync(InitFromRemote);
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

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor
    /// <summary>
    /// Do not use, only for design preview to work properly
    /// </summary>
    public SftpSynchronizerViewModel() { }
#pragma warning restore CS8618
}
