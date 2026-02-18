using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using ReactiveUI;

namespace FileSurfer.Core.ViewModels;

public enum OperationType
{
    Create,
    Delete,
    Update,
}

public record SynchronizationEvent
{
    public required string LocalPath { get; init; }
    public required string RemotePath { get; init; }
    public required OperationType OpType { get; init; }
    public required DateTime TimeStamp { get; init; }
    public string TimeStampStr => TimeStamp.ToLongTimeString();
}

public class SftpSynchronizerViewModel : ReactiveObject
{
    public string LocalDirLabel => LocalDir.FileSystem.GetLabel();
    public Location LocalDir { get; init; } = null!;
    public string RemoteDirLabel => RemoteDir.FileSystem.GetLabel();
    public Location RemoteDir { get; init; } = null!;

    public bool SyncFromRemoteOnStar
    {
        get => _syncFromRemoteOnStar;
        set => this.RaiseAndSetIfChanged(ref _syncFromRemoteOnStar, value);
    }
    private bool _syncFromRemoteOnStar = false;

    public bool Synchronizing
    {
        get => _synchronizing;
        set => this.RaiseAndSetIfChanged(ref _synchronizing, value);
    }
    private bool _synchronizing = false;

    public ObservableCollection<SynchronizationEvent> SyncEvents { get; init; } = [];

    public ReactiveCommand<Unit, Task> StartSyncCommand { get; }
    public ReactiveCommand<Unit, Task> StopSyncCommand { get; }

    public SftpSynchronizerViewModel()
    {
        StartSyncCommand = ReactiveCommand.Create(StartSynchronization);
        StopSyncCommand = ReactiveCommand.Create(StopSynchronization);
    }

    private async Task StartSynchronization() { }

    private async Task StopSynchronization() { }
}
