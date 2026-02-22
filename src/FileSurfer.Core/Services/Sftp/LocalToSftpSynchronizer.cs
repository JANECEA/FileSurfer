using System;
using System.Collections.Generic;
using System.IO;
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
    private readonly Location _localRoot;
    private readonly string _localRootPath;
    private readonly Location _remoteRoot;
    private readonly string _remoteRootPath;

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
        _remoteRoot = remoteRoot;
        _remoteRootPath = PathTools.NormalizePath(remoteRoot.Path);
        _localRoot = localRoot;
        _localRootPath = PathTools.NormalizeLocalPath(localRoot.Path);

        _watcher.ChangeDetected += OnFsEvent;
    }

    public async Task<IResult> StartAsync(bool initFromRemote)
    {
        if (_watcherTask is not null)
            throw new InvalidOperationException("Synchronizer is already running.");

        IResult result = Initialize(initFromRemote);
        if (!result.IsOk)
            return result;

        _cts = new CancellationTokenSource();

        _watcherTask = _watcher.StartAsync(_cts.Token);
        result = await _watcherTask;

        _cts.Dispose();
        _cts = null;
        _watcherTask = null;

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

    private IResult Initialize(bool initFromRemote)
    {
        bool sh = FileSurferSettings.SyncHiddenFiles;
        _watcher.SyncHiddenFiles = sh;

        return initFromRemote
            ? InitializeFrom(_remoteRoot, _localRoot, sh, ToLocalPath, DownloadFile)
            : InitializeFrom(_localRoot, _remoteRoot, sh, ToRemotePath, UploadFile);
    }

    private static IResult InitializeFrom(
        Location rootFrom,
        Location rootTo,
        bool syncHidden,
        Func<string, string> mirrorPath,
        Func<string, string, IResult> handleFile
    )
    {
        IFileSystem fsFrom = rootFrom.FileSystem;
        IFileSystem fsTo = rootTo.FileSystem;

        IResult resetResult = ResetDir(rootTo, syncHidden);
        if (!resetResult.IsOk)
            return resetResult;

        Queue<string> queue = new();
        queue.Enqueue(rootFrom.Path);

        Result result = Result.Ok();
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();

            var dirResult = fsFrom.FileInfoProvider.GetPathDirs(current, syncHidden, false);
            var fileResult = fsFrom.FileInfoProvider.GetPathFiles(current, syncHidden, false);
            if (ResultExtensions.FirstError(dirResult, fileResult) is IResult error)
                return error;

            foreach (DirectoryEntryInfo d in dirResult.Value)
            {
                queue.Enqueue(d.PathToEntry);
                string mirroredPath = mirrorPath(d.PathToEntry);
                result.MergeResult(
                    fsTo.FileIoHandler.NewDirAt(Path.GetDirectoryName(mirroredPath)!, d.Name)
                );
            }

            foreach (FileEntryInfo f in fileResult.Value)
            {
                string mirroredPath = mirrorPath(f.PathToEntry);
                result.MergeResult(handleFile(f.PathToEntry, mirroredPath));
            }
        }
        return result;
    }

    private IResult UploadFile(string localPath, string remotePath) =>
        _remoteHandler.UploadFile(localPath, remotePath);

    private IResult DownloadFile(string remotePath, string localPath) =>
        _remoteHandler.DownloadFile(remotePath, localPath);

    private string ToRemotePath(string localPath)
    {
        localPath = PathTools.NormalizeLocalPath(localPath);

        string relative = localPath[(_localRootPath.Length + 1)..];
        string fwSlashes = relative.Replace(PathTools.DirSeparator, SftpPathTools.DirSeparator);
        return SftpPathTools.Combine(_remoteRootPath, fwSlashes);
    }

    private string ToLocalPath(string remotePath)
    {
        remotePath = PathTools.NormalizePath(remotePath);

        string relative = remotePath[(_remoteRootPath.Length + 1)..];
        string correctSlashes = relative.Replace(
            SftpPathTools.DirSeparator,
            PathTools.DirSeparator
        );
        return Path.Combine(_localRootPath, correctSlashes);
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
