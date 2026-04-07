using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
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

    private readonly IDirectoryWatcher _watcher;
    private readonly IFileIoHandler _remoteHandler;
    private readonly Location _localRoot;
    private readonly string _localRootPath;
    private readonly Location _remoteRoot;
    private readonly string _remoteRootPath;

    private CancellationTokenSource? _cts;
    private Task<IResult>? _syncTask;

    public event SyncEvent? OnSyncEvent;

    public LocalToSftpSynchronizer(
        Location localRoot,
        Location remoteRoot,
        IDirectoryWatcher watcher
    )
    {
        _watcher = watcher;
        _remoteHandler = remoteRoot.FileSystem.FileIoHandler;
        _remoteRoot = remoteRoot;
        _remoteRootPath = RemoteUnixPathTools.NormalizePath(remoteRoot.Path);
        _localRoot = localRoot;
        _localRootPath = LocalPathTools.NormalizePath(localRoot.Path);

        _watcher.ChangeDetected += OnFsEvent;
    }

    public async Task<IResult> StartAsync()
    {
        if (_syncTask is not null)
            return SimpleResult.Error("Synchronizer is already running.");

        bool syncHidden = FileSurferSettings.SyncHiddenFiles;
        TimeSpan pollingInterval = TimeSpan.FromMilliseconds(
            FileSurferSettings.SynchronizerPollingInterval
        );

        _cts = new CancellationTokenSource();

        _syncTask = _watcher.StartAsync(pollingInterval, syncHidden, _cts.Token);
        IResult result = await _syncTask;

        _cts.Dispose();
        _cts = null;
        _syncTask = null;

        return result;
    }

    public Task<IResult> Initialize(
        bool initFromRemote,
        ProgressReporter reporter,
        CancellationToken ct
    ) =>
        Task.Run(() =>
            InitInternal(initFromRemote, FileSurferSettings.SyncHiddenFiles, reporter, ct)
        );

    private async Task<IResult> InitInternal(
        bool initFromRemote,
        bool syncHidden,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        Location from = initFromRemote ? _remoteRoot : _localRoot;
        Location to = initFromRemote ? _localRoot : _remoteRoot;

        Result result = Result.Ok();
        result.MergeResult(await InitializeFrom(from, to, syncHidden, reporter, ct));

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

    private static async Task<IResult> InitializeFrom(
        Location rootFrom,
        Location rootTo,
        bool syncHidden,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        IFileIoHandler toIo = rootTo.FileSystem.FileIoHandler;

        IResult resetResult = ResetDir(rootTo, syncHidden);
        if (!resetResult.IsOk)
            return resetResult;

        ValueResult<DirTransferStream> streamR = DirTransferStream.FromInfoProvider(
            rootFrom.FileSystem.FileInfoProvider,
            rootFrom.Path,
            syncHidden,
            false
        );
        if (!streamR.IsOk)
            return streamR;

        IResult result = SimpleResult.Ok();
        foreach (DirTransferStream dirStream in streamR.Value.Directories.Where(_ => result.IsOk))
            result = await toIo.WriteDirStream(dirStream, rootTo.Path, reporter, ct);

        foreach (FileTransferStream fileStream in streamR.Value.Files.Where(_ => result.IsOk))
            result = await toIo.WriteFileStream(fileStream, rootTo.Path, reporter, ct);

        streamR.Value.Dispose();
        return result;
    }

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
            : await HandleFileEvent(fsEvent, remotePath);

        SyncEvent? eventMethod = OnSyncEvent;
        if (eventMethod is not null)
            await eventMethod(fsEvent, remotePath, result);
    }

    private async Task<IResult> UploadFile(string localPath, string remotePath)
    {
        ValueResult<FileTransferStream> fileStreamR = FileTransferStream.FromInfoProvider(
            _localRoot.FileSystem.FileInfoProvider,
            localPath
        );
        if (!fileStreamR.IsOk)
            return fileStreamR;

        string remoteParent = _remoteRoot.FileSystem.FileInfoProvider.PathTools.GetParentDir(
            remotePath
        );
        return await _remoteRoot.FileSystem.FileIoHandler.WriteFileStream(
            fileStreamR.Value,
            remoteParent,
            ProgressReporter.None,
            _cts!.Token
        );
    }

    private async Task<IResult> HandleFileEvent(FileSystemEvent e, string remotePath) =>
        e.EventType switch
        {
            FileSystemEventType.Created or FileSystemEventType.Updated => await UploadFile(
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
