using System;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Services.Sftp;

public class SftpSynchronizerService
{
    public Action<SynchronizationEvent>? EventCallback { get; set; }
    private CancellationTokenSource _syncCts = new();
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private Location _localDir;
    private Location _remoteDir;

    public SftpSynchronizerService(
        Action<SynchronizationEvent> eventCallback,
        Location localDir,
        Location remoteDir
    )
    {
        EventCallback = eventCallback;
        _localDir = localDir;
        _remoteDir = remoteDir;
    }

    public async Task StartSynchronization()
    {
        await _mutex.WaitAsync();
    }
}
