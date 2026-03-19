using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.FileInformation;
using FileSurfer.Core.Services.FileOperations;

namespace FileSurfer.Core.Services.Sftp;

/// <summary>
/// Synchronizes a local directory to a remote one in real-time by reacting to
/// <see cref="DirectoryWatcher"/> events and replaying them on the remote filesystem.
/// </summary>
public sealed class LocalToSftpSynchronizer : IAsyncDisposable
{
    public delegate Task SyncEvent(FileSystemEvent fsEvent, string remotePath, IResult result);

    private static readonly Task<IResult> InitCancelledResult = Task.FromResult<IResult>(
        SimpleResult.Error("Initialization was cancelled.")
    );

    private readonly IDirectoryWatcher _watcher;
    private readonly IRemoteFileIoHandler _remoteHandler;
    private readonly Location _localRoot;
    private readonly string _localRootPath;
    private readonly Location _remoteRoot;
    private readonly string _remoteRootPath;

    private CancellationTokenSource? _cts;
    private Task<IResult>? _syncTask;

    public event SyncEvent? OnSyncEvent;

    public LocalToSftpSynchronizer(
        Location remoteRoot,
        Location localRoot,
        IDirectoryWatcher watcher,
        IRemoteFileIoHandler remoteHandler
    )
    {
        _watcher = watcher;
        _remoteHandler = remoteHandler;
        _remoteRoot = remoteRoot;
        _remoteRootPath = RemoteUnixPathTools.NormalizePath(remoteRoot.Path);
        _localRoot = localRoot;
        _localRootPath = LocalPathTools.NormalizePath(localRoot.Path);

        _watcher.ChangeDetected += OnFsEvent;
    }

    public async Task<IResult> StartAsync(bool initFromRemote)
    {
        if (_syncTask is not null)
            return SimpleResult.Error("Synchronizer is already running.");

        bool syncHidden = FileSurferSettings.SyncHiddenFiles;
        TimeSpan pollingInterval = TimeSpan.FromMilliseconds(
            FileSurferSettings.SynchronizerPollingInterval
        );

        _cts = new CancellationTokenSource();

        _syncTask = InitAndStart(initFromRemote, syncHidden, pollingInterval);
        IResult result = await _syncTask;

        _cts.Dispose();
        _cts = null;
        _syncTask = null;

        return result;
    }

    private async Task<IResult> InitAndStart(
        bool initFromRemote,
        bool syncHidden,
        TimeSpan pollingInterval
    )
    {
        CancellationToken ct = _cts!.Token;

        IResult result = await Task.Run(() => Initialize(initFromRemote, syncHidden, ct), ct);
        if (!result.IsOk)
            return result;

        return await _watcher.StartAsync(pollingInterval, syncHidden, ct);
    }

    private async Task<IResult> Initialize(
        bool initFromRemote,
        bool syncHidden,
        CancellationToken ct
    )
    {
        Location from = initFromRemote ? _remoteRoot : _localRoot;
        Location to = initFromRemote ? _localRoot : _remoteRoot;
        Func<string, string> mirrorPathF = initFromRemote ? ToLocalPath : ToRemotePath;
        Func<string, string, IResult> handleFile = initFromRemote ? DownloadFile : UploadFile;

        Result result = Result.Ok();
        result.MergeResult(await InitializeFrom(from, to, syncHidden, mirrorPathF, handleFile, ct));
        if (!result.IsOk)
            result.MergeResult(ResetDir(to, syncHidden));

        return result;
    }

    private static IResult ResetDir(Location dir, bool syncHidden)
    {
        IFileSystem fs = dir.FileSystem;

        var dirResult = fs.FileInfoProvider.GetPathDirs(dir.Path, syncHidden, false);
        var fileResult = fs.FileInfoProvider.GetPathFiles(dir.Path, syncHidden, false);
        if (ResultExtensions.FirstError(dirResult, fileResult) is IResult result)
            return result;

        Result rs = Result.Ok();
        foreach (DirectoryEntryInfo d in dirResult.Value)
            rs.MergeResult(fs.FileIoHandler.DeleteDir(d.PathToEntry));

        foreach (FileEntryInfo f in fileResult.Value)
            rs.MergeResult(fs.FileIoHandler.DeleteFile(f.PathToEntry));

        return rs;
    }

    private static Task<IResult> InitializeFrom(
        Location rootFrom,
        Location rootTo,
        bool syncHidden,
        Func<string, string> mirrorPath,
        Func<string, string, IResult> handleFile,
        CancellationToken ct
    )
    {
        IFileSystem fsFrom = rootFrom.FileSystem;
        IFileSystem fsTo = rootTo.FileSystem;
        IPathTools pathToolsTo = fsTo.FileInfoProvider.PathTools;

        IResult resetResult = ResetDir(rootTo, syncHidden);
        if (!resetResult.IsOk)
            return Task.FromResult(resetResult);

        Queue<string> queue = new();
        queue.Enqueue(rootFrom.Path);

        Result result = Result.Ok();
        while (queue.Count > 0)
        {
            if (ct.IsCancellationRequested)
                return InitCancelledResult;

            string current = queue.Dequeue();

            var dirResult = fsFrom.FileInfoProvider.GetPathDirs(current, syncHidden, false);
            var fileResult = fsFrom.FileInfoProvider.GetPathFiles(current, syncHidden, false);
            if (ResultExtensions.FirstError(dirResult, fileResult) is IResult error)
                return Task.FromResult(error);

            foreach (DirectoryEntryInfo d in dirResult.Value)
            {
                if (ct.IsCancellationRequested)
                    return InitCancelledResult;

                queue.Enqueue(d.PathToEntry);
                string mirroredPath = mirrorPath(d.PathToEntry);
                result.MergeResult(
                    fsTo.FileIoHandler.NewDirAt(pathToolsTo.GetParentDir(mirroredPath), d.Name)
                );
            }

            foreach (FileEntryInfo f in fileResult.Value)
            {
                if (ct.IsCancellationRequested)
                    return InitCancelledResult;

                string mirroredPath = mirrorPath(f.PathToEntry);
                result.MergeResult(handleFile(f.PathToEntry, mirroredPath));
            }
        }
        return Task.FromResult<IResult>(result);
    }

    private IResult UploadFile(string localPath, string remotePath) =>
        _remoteHandler.UploadFile(localPath, remotePath);

    private IResult DownloadFile(string remotePath, string localPath) =>
        _remoteHandler.DownloadFile(remotePath, localPath);

    private string ToRemotePath(string localPath)
    {
        localPath = LocalPathTools.NormalizePath(localPath);

        string relative = localPath[(_localRootPath.Length + 1)..];
        if (LocalPathTools.DirSeparator != RemoteUnixPathTools.DirSeparator)
            relative = relative.Replace(
                LocalPathTools.DirSeparator,
                RemoteUnixPathTools.DirSeparator
            );

        return RemoteUnixPathTools.Combine(_remoteRootPath, relative);
    }

    private string ToLocalPath(string remotePath)
    {
        remotePath = RemoteUnixPathTools.NormalizePath(remotePath);

        string relative = remotePath[(_remoteRootPath.Length + 1)..];
        if (RemoteUnixPathTools.DirSeparator != LocalPathTools.DirSeparator)
            relative = relative.Replace(
                RemoteUnixPathTools.DirSeparator,
                LocalPathTools.DirSeparator
            );

        return LocalPathTools.Combine(_localRootPath, relative);
    }

    public async Task StopAsync()
    {
        if (_cts is null)
            return;

        await _cts.CancelAsync();

        try
        {
            if (_syncTask is not null)
                await _syncTask;
        }
        catch (OperationCanceledException)
        {
            // expected on cancellation
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _syncTask = null;
        }
    }

    private async Task OnFsEvent(object? sender, FileSystemEvent fsEvent)
    {
        string remotePath = ToRemotePath(fsEvent.OriginalPath);

        await Task.Yield();

        IResult result = fsEvent.IsDirectory
            ? HandleDirEvent(fsEvent, remotePath)
            : HandleFileEvent(fsEvent, remotePath);

        SyncEvent? eventMethod = OnSyncEvent;
        if (eventMethod is not null)
            await eventMethod(fsEvent, remotePath, result);
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
                RemoteUnixPathTools.GetParentDir(ToRemotePath(newPath))
            ),

            FileSystemEventType.Copied when e.NewPath is string newPath =>
                _remoteHandler.CopyFileTo(
                    remotePath,
                    RemoteUnixPathTools.GetParentDir(ToRemotePath(newPath))
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
                RemoteUnixPathTools.GetParentDir(remotePath),
                RemoteUnixPathTools.GetFileName(remotePath)
            ),

            FileSystemEventType.Deleted => _remoteHandler.DeleteDir(remotePath),

            FileSystemEventType.Moved when e.NewPath is string newPath => _remoteHandler.MoveDirTo(
                remotePath,
                RemoteUnixPathTools.GetParentDir(ToRemotePath(newPath))
            ),

            FileSystemEventType.Copied when e.NewPath is string newPath => _remoteHandler.CopyDirTo(
                remotePath,
                RemoteUnixPathTools.GetParentDir(ToRemotePath(newPath))
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
