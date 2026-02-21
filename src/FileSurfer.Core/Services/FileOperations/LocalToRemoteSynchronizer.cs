using System;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Sftp;
using FileSurfer.Core.Services.FileInformation;

namespace FileSurfer.Core.Services.FileOperations;

/// <summary>
/// Synchronizes a local directory to a remote one in real-time by reacting to
/// <see cref="DirectoryWatcher"/> events and replaying them on the remote filesystem.
/// </summary>
public sealed class LocalToRemoteSynchronizer : IAsyncDisposable
{
    public delegate void SyncEvent(FileSystemEvent fsEvent, string remotePath, IResult result);

    private readonly IDirectoryWatcher _watcher;
    private readonly IRemoteFileIoHandler _remoteHandler;
    private readonly string _localRoot;
    private readonly string _remoteRoot;

    private CancellationTokenSource? _cts;
    private Task? _watcherTask;

    public event SyncEvent? OnSyncEvent;

    public LocalToRemoteSynchronizer(
        IDirectoryWatcher watcher,
        string localRoot,
        string remoteRoot,
        IRemoteFileIoHandler remoteHandler
    )
    {
        _watcher = watcher;
        _localRoot = PathTools.NormalizeLocalPath(localRoot);
        _remoteRoot = PathTools.NormalizePath(remoteRoot);
        _remoteHandler = remoteHandler;

        _watcher.ChangeDetected += OnFsEvent;
    }

    /// <summary>
    /// Starts watching the local directory and syncing changes to the remote.
    /// Safe to call only once; call <see cref="StopAsync"/> before restarting.
    /// </summary>
    public Task StartAsync()
    {
        if (_watcherTask is not null)
            throw new InvalidOperationException("Synchronizer is already running.");

        _cts = new CancellationTokenSource();
        _watcherTask = Task.Run(() => _watcher.StartAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Signals the watcher to stop and waits for the background task to finish.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts is null || _watcherTask is null)
            return;

        await _cts.CancelAsync();

        try
        {
            await _watcherTask;
        }
        catch (OperationCanceledException)
        {
            // expected on cancellation
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _watcherTask = null;
        }
    }

    private void OnFsEvent(object? sender, FileSystemEvent fsEvent)
    {
        string remotePath = ToRemotePath(fsEvent.OriginalPath);

        IResult result = fsEvent.IsDirectory
            ? HandleDirEvent(fsEvent, remotePath)
            : HandleFileEvent(fsEvent, remotePath);

        OnSyncEvent?.Invoke(fsEvent, remotePath, result);
    }

    private IResult HandleFileEvent(FileSystemEvent e, string remotePath) =>
        e.EventType switch
        {
            FileSystemEventType.Created or FileSystemEventType.Updated => _remoteHandler.UploadFile(
                e.OriginalPath,
                remotePath
            ),

            FileSystemEventType.Deleted => _remoteHandler.DeleteFile(remotePath),

            FileSystemEventType.Moved when e.NewPath is string newPath => _remoteHandler.MoveFileTo(
                remotePath,
                SftpPathTools.GetParentDir(ToRemotePath(newPath))
            ),

            FileSystemEventType.Copied when e.NewPath is string newPath =>
                _remoteHandler.CopyFileTo(
                    remotePath,
                    SftpPathTools.GetParentDir(ToRemotePath(newPath))
                ),

            FileSystemEventType.Moved or FileSystemEventType.Copied => SimpleResult.Error(
                $"Missing newPath on {e.EventType} event for '{e.OriginalPath}'."
            ),

            _ => SimpleResult.Error("Unknown event type."),
        };

    private IResult HandleDirEvent(FileSystemEvent e, string remotePath) =>
        e.EventType switch
        {
            FileSystemEventType.Created => _remoteHandler.NewDirAt(
                SftpPathTools.GetParentDir(remotePath),
                SftpPathTools.GetFileName(remotePath)
            ),

            FileSystemEventType.Deleted => _remoteHandler.DeleteDir(remotePath),

            FileSystemEventType.Moved when e.NewPath is string newPath => _remoteHandler.MoveDirTo(
                remotePath,
                SftpPathTools.GetParentDir(ToRemotePath(newPath))
            ),

            FileSystemEventType.Copied when e.NewPath is string newPath => _remoteHandler.CopyDirTo(
                remotePath,
                SftpPathTools.GetParentDir(ToRemotePath(newPath))
            ),

            FileSystemEventType.Moved or FileSystemEventType.Copied => SimpleResult.Error(
                $"Missing newPath on {e.EventType} event for '{e.OriginalPath}'."
            ),

            _ => SimpleResult.Error("Unknown event type."),
        };

    private string ToRemotePath(string localPath)
    {
        string normalized = PathTools.NormalizeLocalPath(localPath);
        string relative = normalized[(_localRoot.Length + 1)..];
        return $"{_remoteRoot}/{relative.Replace(PathTools.DirSeparator, '/')}";
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _watcher.ChangeDetected -= OnFsEvent;
    }
}
