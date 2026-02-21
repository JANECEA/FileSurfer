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
    public event Action<object?, FileSystemEvent>? ChangeDetected;

    public Task<IResult> StartAsync(CancellationToken token);
}

public sealed class DirectoryWatcher : IDirectoryWatcher
{
    private sealed record FsEntryMeta(bool IsDirectory, DateTime LastWriteTimeUtc, long Length);

    private readonly TimeSpan _interval;
    private readonly Location _root;

    private Dictionary<string, FsEntryMeta> _snapshot = new();

    public DirectoryWatcher(Location root, TimeSpan interval)
    {
        _root = root;
        _interval = interval;
    }

    public event Action<object?, FileSystemEvent>? ChangeDetected;

    public async Task<IResult> StartAsync(CancellationToken token)
    {
        ValueResult<Dictionary<string, FsEntryMeta>> firstSnapshotResult = TakeSnapshot();
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

            ValueResult<Dictionary<string, FsEntryMeta>> snapshotResult = TakeSnapshot();
            if (!snapshotResult.IsOk)
                return snapshotResult;

            DiffSnapshotsAsync(_snapshot, snapshotResult.Value);
            _snapshot = snapshotResult.Value;
        }

        return SimpleResult.Ok();
    }

    private ValueResult<Dictionary<string, FsEntryMeta>> TakeSnapshot()
    {
        IFileSystem fs = _root.FileSystem;
        Dictionary<string, FsEntryMeta> snapshot = new();

        Queue<string> queue = new();
        queue.Enqueue(_root.Path);

        while (queue.Count > 0)
        {
            string path = queue.Dequeue();

            var dirResult = fs.FileInfoProvider.GetPathDirs(path, true, true);
            var fileResult = fs.FileInfoProvider.GetPathFiles(path, true, true);

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

    private static IEnumerable<KeyValuePair<string, FsEntryMeta>> OrderEntries(
        Dictionary<string, FsEntryMeta> snapshot,
        bool dirsBeforeFiles
    ) =>
        dirsBeforeFiles
            ? OnlyDirs(snapshot).Concat(OnlyFiles(snapshot))
            : OnlyFiles(snapshot).Concat(OnlyDirs(snapshot));

    private static bool Modified(FsEntryMeta a, FsEntryMeta b) =>
        a.LastWriteTimeUtc != b.LastWriteTimeUtc || a.Length != b.Length;

    private void DiffSnapshotsAsync(
        Dictionary<string, FsEntryMeta> oldSnapshot,
        Dictionary<string, FsEntryMeta> newSnapshot
    )
    {
        foreach ((string path, FsEntryMeta entry) in OrderEntries(newSnapshot, true))
            if (!oldSnapshot.ContainsKey(path))
                RaiseAsync(
                    new FileSystemEvent(path, entry.IsDirectory, FileSystemEventType.Created)
                );

        foreach ((string path, FsEntryMeta entry) in OrderEntries(oldSnapshot, false))
            if (!newSnapshot.ContainsKey(path))
                RaiseAsync(
                    new FileSystemEvent(path, entry.IsDirectory, FileSystemEventType.Deleted)
                );

        foreach ((string filePath, FsEntryMeta entry) in OnlyFiles(newSnapshot))
            if (
                oldSnapshot.TryGetValue(filePath, out FsEntryMeta? oldEntry)
                && Modified(entry, oldEntry)
            )
                RaiseAsync(new FileSystemEvent(filePath, false, FileSystemEventType.Updated));
    }

    private void RaiseAsync(FileSystemEvent fsEvent) => ChangeDetected?.Invoke(this, fsEvent);
}
