using FileSurfer.Core.Models;
using FileSurfer.Core.Services.FileInformation;
using FileSurfer.Core.Services.Sftp;
using Mocks;
using Mocks.Models;
using Mocks.Services;

namespace Tests.Core.ServiceTests;

public sealed class SyncRemoteIoMock : MockFileIoHandler
{
    public IResult DeleteFileResult { get; init; } = SimpleResult.Ok();

    public override IResult DeleteFile(string filePath)
    {
        RecordCall(nameof(DeleteFile), filePath);
        return DeleteFileResult;
    }
}

public sealed class SyncLocalInfoProviderMock : MockFileInfoProvider
{
    public override ValueResult<Stream> GetFileStream(string path)
    {
        RecordCall(nameof(GetFileStream), path);
        return ValueResult<Stream>.Ok(new MemoryStream([1, 2, 3]));
    }
}

public class LocalToSftpSynchronizerTests
{
    public static TheoryData<FileSystemEvent, string, object[]> DirectoryEventCases =>
        new()
        {
            {
                new FileSystemEvent("/local/root/dir-a", true, FileSystemEventType.Created),
                nameof(MockFileIoHandler.NewDirAt),
                ["/remote/root", "dir-a"]
            },
            {
                new FileSystemEvent("/local/root/dir-a", true, FileSystemEventType.Deleted),
                nameof(MockFileIoHandler.DeleteDir),
                ["/remote/root/dir-a"]
            },
            {
                new FileSystemEvent(
                    "/local/root/dir-a",
                    true,
                    FileSystemEventType.Moved,
                    "/local/root/target/dir-a"
                ),
                nameof(MockFileIoHandler.MoveDirTo),
                ["/remote/root/dir-a", "/remote/root/target"]
            },
            {
                new FileSystemEvent(
                    "/local/root/dir-a",
                    true,
                    FileSystemEventType.Copied,
                    "/local/root/target/dir-a"
                ),
                nameof(MockFileIoHandler.CopyDirTo),
                ["/remote/root/dir-a", "/remote/root/target"]
            },
        };

    [Theory]
    [MemberData(nameof(DirectoryEventCases))]
    public async Task SynchronizeAsync_InvokesExpectedDirectoryIoOperation(
        FileSystemEvent fsEvent,
        string expectedMethod,
        object[] expectedArgs
    )
    {
        SyncRemoteIoMock remoteIo = new();
        LocalToSftpSynchronizer synchronizer = CreateSynchronizer([fsEvent], remoteIo);

        IResult result = await synchronizer.SynchronizeAsync(
            TimeSpan.Zero,
            syncHidden: false,
            CancellationToken.None
        );

        Assert.True(result.IsOk);
        Assert.Contains(
            remoteIo.Calls.Where(c => c.Method == expectedMethod),
            c => c.Args.SequenceEqual(expectedArgs)
        );
    }

    public static TheoryData<FileSystemEvent, string, object[]> FileEventCases =>
        new()
        {
            {
                new FileSystemEvent("/local/root/f.txt", false, FileSystemEventType.Deleted),
                nameof(MockFileIoHandler.DeleteFile),
                ["/remote/root/f.txt"]
            },
            {
                new FileSystemEvent(
                    "/local/root/f.txt",
                    false,
                    FileSystemEventType.Moved,
                    "/local/root/target/f.txt"
                ),
                nameof(MockFileIoHandler.MoveFileTo),
                ["/remote/root/f.txt", "/remote/root/target"]
            },
            {
                new FileSystemEvent(
                    "/local/root/f.txt",
                    false,
                    FileSystemEventType.Copied,
                    "/local/root/target/f.txt"
                ),
                nameof(MockFileIoHandler.CopyFileTo),
                ["/remote/root/f.txt", "/remote/root/target"]
            },
        };

    [Theory]
    [MemberData(nameof(FileEventCases))]
    public async Task SynchronizeAsync_InvokesExpectedFileIoOperation(
        FileSystemEvent fsEvent,
        string expectedMethod,
        object[] expectedArgs
    )
    {
        SyncRemoteIoMock remoteIo = new();
        LocalToSftpSynchronizer synchronizer = CreateSynchronizer([fsEvent], remoteIo);

        IResult result = await synchronizer.SynchronizeAsync(
            TimeSpan.Zero,
            syncHidden: false,
            CancellationToken.None
        );

        Assert.True(result.IsOk);
        Assert.Contains(
            remoteIo.Calls.Where(c => c.Method == expectedMethod),
            c => c.Args.SequenceEqual(expectedArgs)
        );
    }

    [Fact]
    public async Task SynchronizeAsync_FileCreated_UploadsToRemoteParentDirectory()
    {
        SyncRemoteIoMock remoteIo = new();
        LocalToSftpSynchronizer synchronizer = CreateSynchronizer(
            [
                new FileSystemEvent(
                    "/local/root/target/file.txt",
                    false,
                    FileSystemEventType.Created
                ),
            ],
            remoteIo
        );

        await synchronizer.SynchronizeAsync(TimeSpan.Zero, false, CancellationToken.None);

        Assert.Contains(
            remoteIo.Calls.Where(c => c.Method == nameof(MockFileIoHandler.WriteFileStreamAsync)),
            c => (string)c.Args[1] == "/remote/root/target"
        );
    }

    public static TheoryData<FileSystemEvent> MissingNewPathCases =>
        new()
        {
            new FileSystemEvent("/local/root/a", true, FileSystemEventType.Moved),
            new FileSystemEvent("/local/root/a", true, FileSystemEventType.Copied),
            new FileSystemEvent("/local/root/a.txt", false, FileSystemEventType.Moved),
            new FileSystemEvent("/local/root/a.txt", false, FileSystemEventType.Copied),
        };

    [Theory]
    [MemberData(nameof(MissingNewPathCases))]
    public async Task SynchronizeAsync_MissingNewPath_ReportsError(FileSystemEvent fsEvent)
    {
        SyncRemoteIoMock remoteIo = new();
        LocalToSftpSynchronizer synchronizer = CreateSynchronizer([fsEvent], remoteIo);
        List<IResult> syncResults = [];
        synchronizer.OnSyncEvent += (_, _, rs) =>
        {
            syncResults.Add(rs);
            return Task.CompletedTask;
        };

        await synchronizer.SynchronizeAsync(TimeSpan.Zero, false, CancellationToken.None);

        IResult result = Assert.Single(syncResults);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task SynchronizeAsync_IoError_IsPropagatedToSyncEvent()
    {
        SyncRemoteIoMock remoteIo = new()
        {
            DeleteFileResult = SimpleResult.Error("delete failed"),
        };
        LocalToSftpSynchronizer synchronizer = CreateSynchronizer(
            [new FileSystemEvent("/local/root/f.txt", false, FileSystemEventType.Deleted)],
            remoteIo
        );
        List<IResult> syncResults = [];
        synchronizer.OnSyncEvent += (_, _, rs) =>
        {
            syncResults.Add(rs);
            return Task.CompletedTask;
        };

        await synchronizer.SynchronizeAsync(TimeSpan.Zero, false, CancellationToken.None);

        IResult result = Assert.Single(syncResults);
        Assert.False(result.IsOk);
    }

    private static LocalToSftpSynchronizer CreateSynchronizer(
        IReadOnlyList<FileSystemEvent> events,
        SyncRemoteIoMock remoteIo
    )
    {
        MockFileSystem localFs = new()
        {
            Local = true,
            FileInfoProvider = new SyncLocalInfoProviderMock(),
        };
        MockFileSystem remoteFs = new()
        {
            Local = false,
            FileIoHandler = remoteIo,
            FileInfoProvider = new MockFileInfoProvider(),
        };

        return new LocalToSftpSynchronizer(
            new Location(localFs, "/local/root"),
            new Location(remoteFs, "/remote/root"),
            new MockDirectoryWatcher(events)
        );
    }
}
