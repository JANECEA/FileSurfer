using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileInformation;
using FileSurfer.Core.Services.FileOperations;
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

public class SftpSynchronizerViewModel : ReactiveObject
{
    private const string SyncErrorTitle = "Synchronization Error";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly LocalToRemoteSynchronizer _synchronizer;
    private readonly IDialogService _dialogService;
    private readonly IDirectoryWatcher _watcher;

    public string LocalDirLabel => LocalDir.FileSystem.GetLabel();
    public Location LocalDir { get; }
    public string RemoteDirLabel => RemoteDir.FileSystem.GetLabel();
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
        SyncHiddenFiles = FileSurferSettings.SyncHiddenFiles;
        LocalDir = localDir;
        RemoteDir = remoteDir;
        _dialogService = dialogService;
        IRemoteFileIoHandler ioHandler = (IRemoteFileIoHandler)remoteDir.FileSystem.FileIoHandler;
        _watcher = new DirectoryWatcher(localDir, Interval);
        _synchronizer = new LocalToRemoteSynchronizer(
            _watcher,
            localDir.Path,
            remoteDir.Path,
            ioHandler
        );
        _synchronizer.OnSyncEvent += ShowEvent;

        StartSyncCommand = ReactiveCommand.Create(StartSynchronization);
        StopSyncCommand = ReactiveCommand.Create(StopSynchronization);
    }

    private void ShowEvent(FileSystemEvent fsEvent, string remotePath, IResult result)
    {
        if (!result.IsOk)
        {
            foreach (string error in result.Errors)
                _dialogService.InfoDialog(SyncErrorTitle, error);

            return;
        }

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
        _watcher.SyncHidden = SyncHiddenFiles;

        SyncEvents.Clear();
        Synchronizing = true;
        await _synchronizer.StartAsync();
    }

    private async Task StopSynchronization()
    {
        await _synchronizer.StopAsync();
        Synchronizing = false;
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor
    /// <summary>
    /// Do not use, only for design preview to work properly
    /// </summary>
    public SftpSynchronizerViewModel() { }
#pragma warning restore CS8618
}
