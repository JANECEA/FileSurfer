using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Sftp;
using FileSurfer.Core.Services.FileInformation;
using FileSurfer.Core.Services.FileOperations;

namespace FileSurfer.Core.Services.Sftp;

/// <summary>
/// Synchronizes a local directory to a remote one in real-time by reacting to
/// <see cref="DirectoryWatcher"/> events and replaying them on the remote filesystem.
/// </summary>
public sealed class LocalToSftpSynchronizer : IAsyncDisposable
{
    public delegate void SyncEvent(FileSystemEvent fsEvent, string remotePath, IResult result);

    private readonly DirectoryWatcher _watcher;
    private readonly IRemoteFileIoHandler _remoteHandler;
    private readonly string _localRoot;
    private readonly string _remoteRoot;

    private CancellationTokenSource? _cts;
    private Task<IResult>? _watcherTask;

    public event SyncEvent? OnSyncEvent;

    public LocalToSftpSynchronizer(
        Location remoteRoot,
        Location localRoot,
        TimeSpan interval,
        IRemoteFileIoHandler remoteHandler
    )
    {
        _watcher = new DirectoryWatcher(localRoot, interval);
        _remoteHandler = remoteHandler;
        _localRoot = PathTools.NormalizeLocalPath(localRoot.Path);
        _remoteRoot = PathTools.NormalizePath(remoteRoot.Path);

        _watcher.ChangeDetected += OnFsEvent;
    }

    public async Task<IResult> StartAsync(bool initFromRemote)
    {
        if (_watcherTask is not null)
            throw new InvalidOperationException("Synchronizer is already running.");

        _cts = new CancellationTokenSource();
        _watcher.SyncHiddenFiles = FileSurferSettings.SyncHiddenFiles;

        _watcherTask = _watcher.StartAsync(_cts.Token);
        IResult result = await _watcherTask;

        _cts.Dispose();
        _cts = null;
        _watcherTask = null;

        return result;
    }

    private IResult InitializeFromLocal(Location root, bool syncHidden)
    {
        IFileSystem fs = root.FileSystem;
        Queue<string> queue = new();
        queue.Enqueue(root.Path);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();

            var dirResult = fs.FileInfoProvider.GetPathDirs(current, syncHidden, false);
            var fileResult = fs.FileInfoProvider.GetPathFiles(current, syncHidden, false);

            if (ResultExtensions.FirstError(dirResult, fileResult) is IResult result)
                return result;

            foreach (DirectoryEntryInfo d in dirResult.Value)
            {
                string remotePath = ToRemotePath(d.PathToEntry);
                queue.Enqueue(d.PathToEntry);
                _remoteHandler.NewDirAt(SftpPathTools.GetParentDir(remotePath), d.Name);
            }

            foreach (FileEntryInfo f in fileResult.Value)
            {
                string remotePath = ToRemotePath(f.PathToEntry);
                _remoteHandler.UploadFile(f.PathToEntry, remotePath);
            }
        }
        return SimpleResult.Ok();
    }

    public async Task StopAsync()
    {
        if (_cts is null)
            return;

        await _cts.CancelAsync();

        try
        {
            if (_watcherTask is not null)
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

    private string ToRemotePath(string localPath)
    {
        string normalized = PathTools.NormalizeLocalPath(localPath);
        string relative = normalized[(_localRoot.Length + 1)..];
        string fwSlashes = relative.Replace(PathTools.DirSeparator, SftpPathTools.DirSeparator);
        return $"{_remoteRoot}{SftpPathTools.DirSeparator}{fwSlashes}";
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

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _watcher.ChangeDetected -= OnFsEvent;
    }
}
