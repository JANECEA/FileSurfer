using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;

namespace FileSurfer.Core.Services.FileInformation;

public enum FileSystemEventType
{
    Created,
    Deleted,
    Updated,
    Moved,
    Copied,
}

public sealed record FileSystemEvent(
    string OriginalPath,
    bool IsDirectory,
    FileSystemEventType EventType,
    string? NewPath = null
);

public interface IDirectoryWatcher
{
    public bool SyncHiddenFiles { get; set; }

    public event Func<object?, FileSystemEvent, Task>? ChangeDetected;

    public Task<IResult> StartAsync(CancellationToken token);
}

public sealed class DirectoryWatcher : IDirectoryWatcher
{
    private sealed record FsEntryMeta(bool IsDirectory, DateTime LastWriteTimeUtc, long Length);

    private readonly TimeSpan _interval;
    private readonly Location _root;
    private Dictionary<string, FsEntryMeta> _snapshot = new();

    public bool SyncHiddenFiles { get; set; } = false;
    public event Func<object?, FileSystemEvent, Task>? ChangeDetected;

    public DirectoryWatcher(Location root, TimeSpan interval)
    {
        _root = root;
        _interval = interval;
    }

    public async Task<IResult> StartAsync(CancellationToken token)
    {
        bool syncHidden = SyncHiddenFiles;
        ValueResult<Dictionary<string, FsEntryMeta>> firstSnapshotResult = TakeSnapshot(syncHidden);
        if (!firstSnapshotResult.IsOk)
            return firstSnapshotResult;

        _snapshot = firstSnapshotResult.Value;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, token);
            }
            catch (TaskCanceledException)
            {
                break; // The task has been canceled.
            }

            ValueResult<Dictionary<string, FsEntryMeta>> snapshotResult = TakeSnapshot(syncHidden);
            if (!snapshotResult.IsOk)
                return snapshotResult;

            await DiffSnapshotsAsync(_snapshot, snapshotResult.Value);
            _snapshot = snapshotResult.Value;
        }
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

            var dirResult = fs.FileInfoProvider.GetPathDirs(path, syncHidden, false);
            var fileResult = fs.FileInfoProvider.GetPathFiles(path, syncHidden, false);

            if (ResultExtensions.FirstError(dirResult, fileResult) is IResult result)
                return ValueResult<Dictionary<string, FsEntryMeta>>.Error(result);

            foreach (DirectoryEntryInfo dir in dirResult.Value)
            {
                snapshot[dir.PathToEntry] = new FsEntryMeta(true, dir.LastModifiedUtc, 0);
                queue.Enqueue(dir.PathToEntry);
            }

            foreach (FileEntryInfo file in fileResult.Value)
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

    private async Task RaiseAsync(FileSystemEvent fsEvent)
    {
        if (ChangeDetected is not null)
            await ChangeDetected.Invoke(this, fsEvent);
    }
}
