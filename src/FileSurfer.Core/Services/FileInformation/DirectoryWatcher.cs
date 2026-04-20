using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;

namespace FileSurfer.Core.Services.FileInformation;

/// <summary>
/// Describes the kind of change detected for a file-system entry.
/// </summary>
public enum FileSystemEventType
{
    /// <summary>
    /// A new entry was created.
    /// </summary>
    Created,

    /// <summary>
    /// An existing entry was deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// An existing entry content or metadata changed.
    /// </summary>
    Updated,

    /// <summary>
    /// An entry was moved to a different path.
    /// </summary>
    Moved,

    /// <summary>
    /// An entry was copied to a different path.
    /// </summary>
    Copied,
}

/// <summary>
/// Represents a file-system change event raised by <see cref="IDirectoryWatcher"/>.
/// </summary>
/// <param name="OriginalPath">
/// Path of the entry where the change was detected.
/// </param>
/// <param name="IsDirectory">
/// Indicates whether the changed entry is a directory.
/// </param>
/// <param name="EventType">
/// The detected change type.
/// </param>
/// <param name="NewPath">
/// Optional destination path for move/copy events.
/// </param>
public sealed record FileSystemEvent(
    string OriginalPath,
    bool IsDirectory,
    FileSystemEventType EventType,
    string? NewPath = null
);

/// <summary>
/// Defines polling-based directory change detection and notification behavior.
/// </summary>
public interface IDirectoryWatcher
{
    /// <summary>
    /// Occurs when a file-system change is detected.
    /// </summary>
    public event Func<FileSystemEvent, Task>? ChangeDetected;

    /// <summary>
    /// Starts watching for changes and keeps polling until cancellation is requested.
    /// </summary>
    /// <param name="pollingInterval">
    /// Delay between snapshot comparisons.
    /// </param>
    /// <param name="syncHidden">
    /// Indicates whether hidden entries should be included when building snapshots.
    /// </param>
    /// <param name="token">
    /// Cancellation token used to stop the watcher loop.
    /// </param>
    /// <returns>
    /// A task that returns the watcher result, including any polling or snapshot errors.
    /// </returns>
    public Task<IResult> StartAsync(
        TimeSpan pollingInterval,
        bool syncHidden,
        CancellationToken token
    );
}

/// <summary>
/// Polls a root directory, compares snapshots, and raises normalized change events.
/// </summary>
public sealed class DirectoryWatcher : IDirectoryWatcher
{
    private sealed record FsEntryMeta(bool IsDirectory, DateTime LastWriteTimeUtc, long Length);

    private readonly Location _root;
    private Dictionary<string, FsEntryMeta> _snapshot = new();

    public event Func<FileSystemEvent, Task>? ChangeDetected;

    /// <summary>
    /// Initializes a directory watcher rooted at the provided location.
    /// </summary>
    /// <param name="root">
    /// Root location to monitor for recursive file-system changes.
    /// </param>
    public DirectoryWatcher(Location root) => _root = root;

    public async Task<IResult> StartAsync(
        TimeSpan pollingInterval,
        bool syncHidden,
        CancellationToken token
    )
    {
        ValueResult<Dictionary<string, FsEntryMeta>> firstSnapshotResult = TakeSnapshot(syncHidden);
        if (!firstSnapshotResult.IsOk)
            return firstSnapshotResult;

        _snapshot = firstSnapshotResult.Value;

        Task<IResult> comparisonTask = Task.FromResult<IResult>(SimpleResult.Ok());
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(pollingInterval, token);
            }
            catch
            {
                break; // The task has been canceled.
            }

            if (comparisonTask.IsCompleted)
            {
                if (!comparisonTask.Result.IsOk)
                    return comparisonTask.Result;

                comparisonTask = Task.Run(() => DiffOnceAsync(syncHidden));
            }
        }
        return await comparisonTask;
    }

    private async Task<IResult> DiffOnceAsync(bool syncHidden)
    {
        ValueResult<Dictionary<string, FsEntryMeta>> snapshotResult = TakeSnapshot(syncHidden);
        if (!snapshotResult.IsOk)
            return snapshotResult;

        await DiffSnapshotsAsync(_snapshot, snapshotResult.Value);
        _snapshot = snapshotResult.Value;
        return SimpleResult.Ok();
    }

    private ValueResult<Dictionary<string, FsEntryMeta>> TakeSnapshot(bool syncHidden)
    {
        IFileSystem fs = _root.FileSystem;
        Dictionary<string, FsEntryMeta> snapshot = new();

        Queue<string> queue = new();
        queue.Enqueue(_root.Path);

        while (queue.Count > 0)
        {
            string path = queue.Dequeue();

            var entriesR = fs.FileInfoProvider.GetPathEntries(path, syncHidden, false);
            if (!entriesR.IsOk)
                return ValueResult<Dictionary<string, FsEntryMeta>>.Error(entriesR);

            foreach (DirectoryEntryInfo dir in entriesR.Value.Dirs)
            {
                snapshot[dir.PathToEntry] = new FsEntryMeta(true, dir.LastModifiedUtc, 0);
                queue.Enqueue(dir.PathToEntry);
            }

            foreach (FileEntryInfo file in entriesR.Value.Files)
                snapshot[file.PathToEntry] = new FsEntryMeta(
                    false,
                    file.LastModifiedUtc,
                    file.SizeB
                );
        }

        return snapshot.OkResult();
    }

    private static IEnumerable<KeyValuePair<string, FsEntryMeta>> OnlyDirs(
        Dictionary<string, FsEntryMeta> snapshot
    ) => snapshot.Where(kp => kp.Value.IsDirectory);

    private static IEnumerable<KeyValuePair<string, FsEntryMeta>> OnlyFiles(
        Dictionary<string, FsEntryMeta> snapshot
    ) => snapshot.Where(kp => !kp.Value.IsDirectory);

    private static IEnumerable<KeyValuePair<string, FsEntryMeta>> DirsFirst(
        Dictionary<string, FsEntryMeta> snapshot
    ) => OnlyDirs(snapshot).Concat(OnlyFiles(snapshot));

    private static IEnumerable<KeyValuePair<string, FsEntryMeta>> FilesFirst(
        Dictionary<string, FsEntryMeta> snapshot
    ) => OnlyFiles(snapshot).Concat(OnlyDirs(snapshot));

    private static bool Modified(FsEntryMeta a, FsEntryMeta b) =>
        a.LastWriteTimeUtc != b.LastWriteTimeUtc || a.Length != b.Length;

    private async Task DiffSnapshotsAsync(
        Dictionary<string, FsEntryMeta> oldSnapshot,
        Dictionary<string, FsEntryMeta> newSnapshot
    )
    {
        foreach ((string path, FsEntryMeta entry) in DirsFirst(newSnapshot))
            if (!oldSnapshot.ContainsKey(path))
                await RaiseAsync(
                    new FileSystemEvent(path, entry.IsDirectory, FileSystemEventType.Created)
                );

        foreach ((string path, FsEntryMeta entry) in FilesFirst(oldSnapshot))
            if (!newSnapshot.ContainsKey(path))
                await RaiseAsync(
                    new FileSystemEvent(path, entry.IsDirectory, FileSystemEventType.Deleted)
                );

        foreach ((string filePath, FsEntryMeta entry) in OnlyFiles(newSnapshot))
            if (
                oldSnapshot.TryGetValue(filePath, out FsEntryMeta? oldEntry)
                && Modified(entry, oldEntry)
            )
                await RaiseAsync(new FileSystemEvent(filePath, false, FileSystemEventType.Updated));
    }

    private Task RaiseAsync(FileSystemEvent fsEvent)
    {
        Func<FileSystemEvent, Task>? eventMethod = ChangeDetected;

        if (eventMethod is null)
            return Task.CompletedTask;

        return eventMethod(fsEvent);
    }
}
