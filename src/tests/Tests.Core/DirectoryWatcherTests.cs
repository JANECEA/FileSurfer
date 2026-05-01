using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.FileInformation;
using Mocks;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Tests.Core;

internal sealed class DirectoryWatcherFileInfoProviderMock : MockFileInfoProvider
{
    private readonly string _rootPath;
    private readonly Queue<ValueResult<Dictionary<string, DirectoryContents>>> _snapshots;
    private Dictionary<string, DirectoryContents>? _activeSnapshot;

    public List<(string Path, bool IncludeHidden, bool IncludeOs)> Requests { get; } = [];

    public DirectoryWatcherFileInfoProviderMock(
        string rootPath,
        params ValueResult<Dictionary<string, DirectoryContents>>[] snapshots
    )
    {
        _rootPath = rootPath;
        _snapshots = new Queue<ValueResult<Dictionary<string, DirectoryContents>>>(snapshots);
    }

    public override Task<ValueResult<DirectoryContents>> GetPathEntriesAsync(
        string path,
        bool includeHidden,
        bool includeOs,
        CancellationToken ct
    )
    {
        RecordCall(nameof(GetPathEntriesAsync), path, includeHidden, includeOs, ct);
        Requests.Add((path, includeHidden, includeOs));

        if (path == _rootPath)
        {
            if (_snapshots.Count == 0)
                return Task.FromResult(
                    ValueResult<DirectoryContents>.Error("No configured snapshot available.")
                );

            ValueResult<Dictionary<string, DirectoryContents>> snapshotResult =
                _snapshots.Dequeue();
            if (!snapshotResult.IsOk)
                return Task.FromResult(ValueResult<DirectoryContents>.Error(snapshotResult));

            _activeSnapshot = snapshotResult.Value;
        }

        if (_activeSnapshot is null)
            return Task.FromResult(
                ValueResult<DirectoryContents>.Error("No active snapshot is available.")
            );

        if (!_activeSnapshot.TryGetValue(path, out DirectoryContents? contents))
            return Task.FromResult(
                ValueResult<DirectoryContents>.Error($"Path '{path}' was not configured.")
            );

        return Task.FromResult(ValueResult<DirectoryContents>.Ok(contents));
    }
}

public class DirectoryWatcherTests
{
    [Fact]
    public async Task StartAsync_ReturnsError_WhenInitialSnapshotFails()
    {
        const string rootPath = "/root";
        DirectoryWatcherFileInfoProviderMock fileInfoProvider = new(
            rootPath,
            ValueResult<Dictionary<string, DirectoryContents>>.Error("Initial snapshot failed.")
        );
        DirectoryWatcher watcher = new(
            new Location(new MockFileSystem { FileInfoProvider = fileInfoProvider }, rootPath),
            (_, _) => Task.CompletedTask
        );

        int changeEvents = 0;
        watcher.ChangeDetected += _ =>
        {
            changeEvents++;
            return Task.CompletedTask;
        };

        IResult result = await watcher.StartAsync(TimeSpan.Zero, false, CancellationToken.None);

        Assert.False(result.IsOk);
        Assert.Equal(0, changeEvents);
    }

    [Fact]
    public async Task StartAsync_Raises_CreatedDeletedAndUpdated_Events()
    {
        const string rootPath = "/root";
        DateTime t1 = DateTime.UnixEpoch;
        DateTime t2 = t1.AddSeconds(1);

        Dictionary<string, DirectoryContents> snapshotA = new()
        {
            [rootPath] = new DirectoryContents
            {
                Dirs = [MockHelper.Dir("/root/dirA", t1)],
                Files = [MockHelper.File("/root/old.txt", 10, t1)],
            },
            ["/root/dirA"] = new DirectoryContents
            {
                Dirs = [],
                Files = [MockHelper.File("/root/dirA/nested.txt", 1, t1)],
            },
        };
        Dictionary<string, DirectoryContents> snapshotB = new()
        {
            [rootPath] = new DirectoryContents
            {
                Dirs = [MockHelper.Dir("/root/dirB", t2)],
                Files =
                [
                    MockHelper.File("/root/old.txt", 20, t2),
                    MockHelper.File("/root/new.txt", 2, t2),
                ],
            },
            ["/root/dirB"] = new DirectoryContents { Dirs = [], Files = [] },
        };

        DirectoryWatcherFileInfoProviderMock fileInfoProvider = new(
            rootPath,
            ValueResult<Dictionary<string, DirectoryContents>>.Ok(snapshotA),
            ValueResult<Dictionary<string, DirectoryContents>>.Ok(snapshotB)
        );
        DirectoryWatcher watcher = new(
            new Location(new MockFileSystem { FileInfoProvider = fileInfoProvider }, rootPath),
            BuildDelayEngine(2)
        );

        List<FileSystemEvent> events = [];
        watcher.ChangeDetected += fsEvent =>
        {
            events.Add(fsEvent);
            return Task.CompletedTask;
        };

        await watcher.StartAsync(TimeSpan.Zero, false, CancellationToken.None);

        Assert.Collection(
            events,
            ev =>
            {
                Assert.Equal(FileSystemEventType.Created, ev.EventType);
                Assert.True(ev.IsDirectory);
                Assert.Equal("/root/dirB", ev.OriginalPath);
            },
            ev =>
            {
                Assert.Equal(FileSystemEventType.Created, ev.EventType);
                Assert.False(ev.IsDirectory);
                Assert.Equal("/root/new.txt", ev.OriginalPath);
            },
            ev =>
            {
                Assert.Equal(FileSystemEventType.Deleted, ev.EventType);
                Assert.False(ev.IsDirectory);
                Assert.Equal("/root/dirA/nested.txt", ev.OriginalPath);
            },
            ev =>
            {
                Assert.Equal(FileSystemEventType.Deleted, ev.EventType);
                Assert.True(ev.IsDirectory);
                Assert.Equal("/root/dirA", ev.OriginalPath);
            },
            ev =>
            {
                Assert.Equal(FileSystemEventType.Updated, ev.EventType);
                Assert.False(ev.IsDirectory);
                Assert.Equal("/root/old.txt", ev.OriginalPath);
            }
        );
    }

    [Fact]
    public async Task StartAsync_PassesSyncHiddenFlagToSnapshotReads()
    {
        const string rootPath = "/root";
        ValueResult<Dictionary<string, DirectoryContents>> emptySnapshot = ValueResult<
            Dictionary<string, DirectoryContents>
        >.Ok(
            new Dictionary<string, DirectoryContents>
            {
                [rootPath] = new() { Dirs = [], Files = [] },
            }
        );

        DirectoryWatcherFileInfoProviderMock fileInfoProvider = new(
            rootPath,
            emptySnapshot,
            emptySnapshot
        );
        DirectoryWatcher watcher = new(
            new Location(new MockFileSystem { FileInfoProvider = fileInfoProvider }, rootPath),
            BuildDelayEngine(2)
        );

        await watcher.StartAsync(TimeSpan.Zero, true, CancellationToken.None);

        Assert.NotEmpty(fileInfoProvider.Requests);
        Assert.All(fileInfoProvider.Requests, req => Assert.True(req.IncludeHidden));
        Assert.All(fileInfoProvider.Requests, req => Assert.False(req.IncludeOs));
    }

    [Fact]
    public async Task StartAsync_ReturnsComparisonError_WhenLaterSnapshotFails()
    {
        const string rootPath = "/root";
        ValueResult<Dictionary<string, DirectoryContents>> firstSnapshot = ValueResult<
            Dictionary<string, DirectoryContents>
        >.Ok(
            new Dictionary<string, DirectoryContents>
            {
                [rootPath] = new() { Dirs = [], Files = [] },
            }
        );

        DirectoryWatcherFileInfoProviderMock fileInfoProvider = new(
            rootPath,
            firstSnapshot,
            ValueResult<Dictionary<string, DirectoryContents>>.Error("Comparison snapshot failed.")
        );
        DirectoryWatcher watcher = new(
            new Location(new MockFileSystem { FileInfoProvider = fileInfoProvider }, rootPath),
            BuildDelayEngine(3)
        );

        IResult result = await watcher.StartAsync(TimeSpan.Zero, false, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    private static Func<TimeSpan, CancellationToken, Task> BuildDelayEngine(
        int successfulCallsBeforeStop
    )
    {
        int calls = 0;
        return (_, _) =>
        {
            calls++;
            if (calls <= successfulCallsBeforeStop)
                return Task.CompletedTask;

            throw new TaskCanceledException();
        };
    }
}
