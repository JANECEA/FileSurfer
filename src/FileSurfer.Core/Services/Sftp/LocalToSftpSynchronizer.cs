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
/// Represents a callback invoked after each synchronized file-system event is processed.
/// </summary>
/// <param name="fsEvent">
/// The original local file-system event received from the watcher.
/// </param>
/// <param name="remotePath">
/// The mapped remote path targeted by the synchronization operation.
/// </param>
/// <param name="result">
/// The operation result produced while handling the event.
/// </param>
/// <returns>
/// A task that completes when event handling notifications have finished.
/// </returns>
public delegate Task SyncEvent(FileSystemEvent fsEvent, string remotePath, IResult result);

/// <summary>
/// Synchronizes a local directory to a remote one in real-time by reacting to
/// <see cref="DirectoryWatcher"/> events and replaying them on the remote filesystem.
/// </summary>
public sealed class LocalToSftpSynchronizer : IDisposable
{
    private readonly IDirectoryWatcher _watcher;
    private readonly IFileIoHandler _remoteHandler;
    private readonly Location _localRoot;
    private readonly string _localRootPath;
    private readonly Location _remoteRoot;
    private readonly string _remoteRootPath;

    private Task _syncTask = Task.CompletedTask;
    private CancellationToken _runningToken = CancellationToken.None;

    /// <summary>
    /// Occurs after a local event is applied (or fails to apply) on the remote side.
    /// </summary>
    public event SyncEvent? OnSyncEvent;

    /// <summary>
    /// Initializes a synchronizer that mirrors changes between the specified local and remote roots.
    /// </summary>
    /// <param name="localRoot">
    /// The local root location to watch for changes.
    /// </param>
    /// <param name="remoteRoot">
    /// The remote SFTP root location to update in response to local changes.
    /// </param>
    /// <param name="watcher">
    /// The directory watcher responsible for raising local file-system events.
    /// </param>
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

        _watcher.ChangeDetected += OnFsEventAsync;
    }

    /// <summary>
    /// Starts continuous synchronization by running the directory watcher loop.
    /// </summary>
    /// <param name="ct">
    /// Cancellation token used to stop synchronization.
    /// </param>
    /// <returns>
    /// A task that returns the watcher/synchronization result when the loop ends.
    /// </returns>
    public async Task<IResult> SynchronizeAsync(CancellationToken ct)
    {
        if (!_syncTask.IsCompleted)
            return SimpleResult.Error("Synchronizer is already running.");

        bool syncHidden = FileSurferSettings.SyncHiddenFiles;
        TimeSpan pollingInterval = TimeSpan.FromMilliseconds(
            FileSurferSettings.SynchronizerPollingInterval
        );

        _runningToken = ct;
        Task<IResult> task = _watcher.StartAsync(pollingInterval, syncHidden, ct);
        _syncTask = task;
        try
        {
            return await task;
        }
        finally
        {
            _runningToken = CancellationToken.None;
        }
    }

    /// <summary>
    /// Performs one-time initial synchronization from one side to the other before live syncing starts.
    /// </summary>
    /// <param name="initFromRemote">
    /// When <see langword="true"/>, initializes local data from the remote root; otherwise initializes
    /// remote data from the local root.
    /// </param>
    /// <param name="reporter">
    /// Progress reporter used for long-running transfer operations.
    /// </param>
    /// <param name="ct">
    /// Cancellation token used to abort initialization.
    /// </param>
    /// <returns>
    /// A task that returns the combined initialization result.
    /// </returns>
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

        var entriesR = fs.FileInfoProvider.GetPathEntries(dir.Path, syncHidden, false);
        if (!entriesR.IsOk)
            return entriesR;

        Result rs = Result.Ok();
        foreach (DirectoryEntryInfo d in entriesR.Value.Dirs)
            rs.MergeResult(fs.FileIoHandler.DeleteDir(d.PathToEntry));

        foreach (FileEntryInfo f in entriesR.Value.Files)
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
            result = await toIo.WriteDirStreamAsync(dirStream, rootTo.Path, reporter, ct);

        foreach (FileTransferStream fileStream in streamR.Value.Files.Where(_ => result.IsOk))
            result = await toIo.WriteFileStreamAsync(fileStream, rootTo.Path, reporter, ct);

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

    private async Task OnFsEventAsync(FileSystemEvent fsEvent)
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

        string remoteParent = _remoteRoot.PathTools().GetParentDir(remotePath);
        try
        {
            return await _remoteRoot.FileSystem.FileIoHandler.WriteFileStreamAsync(
                fileStreamR.Value,
                remoteParent,
                ProgressReporter.None,
                _runningToken
            );
        }
        finally
        {
            fileStreamR.Value.Dispose();
        }
    }

    private Task<IResult> HandleFileEvent(FileSystemEvent e, string remotePath) =>
        e.EventType switch
        {
            FileSystemEventType.Created or FileSystemEventType.Updated => UploadFile(
                e.OriginalPath,
                remotePath
            ),

            FileSystemEventType.Deleted => _remoteHandler.DeleteFile(remotePath).ToTask(),

            FileSystemEventType.Moved when e.NewPath is string newPath => _remoteHandler
                .MoveFileTo(remotePath, RemoteUnixPathTools.GetParentDir(ToRemotePath(newPath)))
                .ToTask(),

            FileSystemEventType.Copied when e.NewPath is string newPath => _remoteHandler
                .CopyFileTo(remotePath, RemoteUnixPathTools.GetParentDir(ToRemotePath(newPath)))
                .ToTask(),

            FileSystemEventType.Moved or FileSystemEventType.Copied => SimpleResult
                .Error($"Missing newPath on {e.EventType} event for '{e.OriginalPath}'.")
                .ToTask(),

            _ => SimpleResult.Error("Unknown event type.").ToTask(),
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

    public void Dispose() => _watcher.ChangeDetected -= OnFsEventAsync;
}
