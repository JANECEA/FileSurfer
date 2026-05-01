using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations.Undoable;
using Mocks;

namespace Tests.Core;

public sealed class FlattenFileIoMock : MockFileIoHandler
{
    public IResult RenameDirAtResult { get; set; } = SimpleResult.Ok();
    public IResult MoveFileToResult { get; set; } = SimpleResult.Ok();

    public override IResult RenameDirAt(string dirPath, string newName)
    {
        RecordCall(nameof(RenameDirAt), dirPath, newName);
        return RenameDirAtResult;
    }

    public override IResult MoveFileTo(string filePath, string destinationDir)
    {
        RecordCall(nameof(MoveFileTo), filePath, destinationDir);
        return MoveFileToResult;
    }
}

public sealed class FlattenInfoProviderMock : MockFileInfoProvider
{
    private readonly Dictionary<string, ExistsInfo> _exists = [];
    private readonly Dictionary<string, DirectoryContents> _entries = [];

    public HashSet<string> PathEntryErrors { get; } = [];

    public override IPathTools PathTools => LocalPathTools.Instance;

    public void SetExists(string path, bool existsAsPath) =>
        _exists[path] = existsAsPath ? ExistsInfo.ExistsAsFile() : ExistsInfo.DoesNotExist();

    public void SetPathEntries(
        string path,
        IReadOnlyList<DirectoryEntryInfo> dirs,
        IReadOnlyList<FileEntryInfo> files
    ) => _entries[path] = new DirectoryContents { Dirs = dirs, Files = files };

    public override ExistsInfo Exists(string path)
    {
        RecordCall(nameof(Exists), path);
        return _exists.GetValueOrDefault(path, ExistsInfo.DoesNotExist());
    }

    public override Task<ValueResult<DirectoryContents>> GetPathEntriesAsync(
        string path,
        bool includeHidden,
        bool includeOs,
        CancellationToken ct
    )
    {
        RecordCall(nameof(GetPathEntriesAsync), path, includeHidden, includeOs, ct);

        if (PathEntryErrors.Contains(path))
            return Task.FromResult(
                ValueResult<DirectoryContents>.Error("Configured path entries failure.")
            );

        if (!_entries.TryGetValue(path, out DirectoryContents? contents))
            return Task.FromResult(
                ValueResult<DirectoryContents>.Error($"No entries configured for '{path}'.")
            );

        return Task.FromResult(ValueResult<DirectoryContents>.Ok(contents));
    }
}

public class FlattenFolderTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsError_ForTopLevelDirectory()
    {
        FlattenFileIoMock io = new();
        FlattenInfoProviderMock info = new();
        FlattenFolder operation = new(io, info, "/");

        IResult result = await operation.InvokeAsync(ProgressReporter.None, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    public static TheoryData<string> InvokeFailureStages => ["rename", "entries", "move"];

    [Theory]
    [MemberData(nameof(InvokeFailureStages))]
    public async Task InvokeAsync_StopsOnExpectedFailureStage(string stage)
    {
        const string dirPath = "/parent/child";
        FlattenFileIoMock io = new();
        FlattenInfoProviderMock info = BuildBaseInfoProvider();
        ConfigureInvokeFailure(stage, io, info, dirPath);
        FlattenFolder operation = new(io, info, dirPath);

        IResult result = await operation.InvokeAsync(ProgressReporter.None, CancellationToken.None);

        Assert.False(result.IsOk);
        Assert.DoesNotContain(io.Calls, call => call.Method == nameof(FlattenFileIoMock.DeleteDir));
    }

    [Fact]
    public async Task InvokeAsync_MovesEntriesAndDeletesSource_WhenSuccessful()
    {
        const string dirPath = "/parent/child";
        FlattenFileIoMock io = new();
        FlattenInfoProviderMock info = BuildBaseInfoProvider();
        FlattenFolder operation = new(io, info, dirPath);

        IResult result = await operation.InvokeAsync(ProgressReporter.None, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.Contains(
            io.Calls.Where(call => call.Method == nameof(FlattenFileIoMock.MoveFileTo)),
            call =>
                (string)call.Args[0] == "/parent/child/a.txt" && (string)call.Args[1] == "/parent"
        );
        Assert.Contains(
            io.Calls.Where(call => call.Method == nameof(FlattenFileIoMock.MoveDirTo)),
            call => (string)call.Args[0] == "/parent/child/sub" && (string)call.Args[1] == "/parent"
        );
        MethodCall deleteCall = Assert.Single(
            io.Calls,
            call => call.Method == nameof(FlattenFileIoMock.DeleteDir)
        );
        Assert.Equal("/parent/child", (string)deleteCall.Args[0]);
    }

    [Fact]
    public async Task UndoAsync_ReturnsError_WhenOriginalPathExistsAndNameNotInMovedEntries()
    {
        const string dirPath = "/parent/child";
        FlattenFileIoMock io = new();
        FlattenInfoProviderMock info = BuildBaseInfoProvider();
        FlattenFolder operation = new(io, info, dirPath);

        IResult invokeResult = await operation.InvokeAsync(
            ProgressReporter.None,
            CancellationToken.None
        );
        Assert.True(invokeResult.IsOk);

        info.SetExists(dirPath, true);

        IResult undoResult = await operation.UndoAsync(
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.False(undoResult.IsOk);
        Assert.DoesNotContain(io.Calls, call => call.Method == nameof(FlattenFileIoMock.NewDirAt));
    }

    [Fact]
    public async Task UndoAsync_CreatesTempDirAndRenamesBack_WhenNameConflictCanBeResolved()
    {
        const string originalDirPath = "/parent/child";
        const string renamedDirPath = "/parent/child (1)";

        FlattenFileIoMock io = new();
        FlattenInfoProviderMock info = new();
        info.SetExists("/parent/child/child", true);
        info.SetExists("/parent/child", true);
        info.SetExists("/parent/child (1)", false);
        info.SetPathEntries(
            renamedDirPath,
            dirs: [Dir("/parent/child (1)/child")],
            files: [File("/parent/child (1)/a.txt")]
        );

        FlattenFolder operation = new(io, info, originalDirPath);
        IResult invokeResult = await operation.InvokeAsync(
            ProgressReporter.None,
            CancellationToken.None
        );
        Assert.True(invokeResult.IsOk);

        info.SetExists(originalDirPath, true);
        info.SetExists("/parent/child (1)", false);

        IResult undoResult = await operation.UndoAsync(
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.True(undoResult.IsOk);
        Assert.Contains(
            io.Calls.Where(call => call.Method == nameof(FlattenFileIoMock.NewDirAt)),
            call => (string)call.Args[0] == "/parent" && (string)call.Args[1] == "child (1)"
        );
        Assert.Contains(
            io.Calls.Where(call => call.Method == nameof(FlattenFileIoMock.MoveDirTo)),
            call =>
                (string)call.Args[0] == "/parent/child"
                && (string)call.Args[1] == "/parent/child (1)"
        );
        Assert.Contains(
            io.Calls.Where(call => call.Method == nameof(FlattenFileIoMock.MoveFileTo)),
            call =>
                (string)call.Args[0] == "/parent/a.txt"
                && (string)call.Args[1] == "/parent/child (1)"
        );
        Assert.Contains(
            io.Calls.Where(call => call.Method == nameof(FlattenFileIoMock.RenameDirAt)),
            call => (string)call.Args[0] == "/parent/child (1)" && (string)call.Args[1] == "child"
        );
    }

    private static void ConfigureInvokeFailure(
        string stage,
        FlattenFileIoMock io,
        FlattenInfoProviderMock info,
        string dirPath
    )
    {
        switch (stage)
        {
            case "rename":
                info.SetExists("/parent/child/child", true);
                info.SetExists("/parent/child", true);
                info.SetExists("/parent/child (1)", false);
                io.RenameDirAtResult = SimpleResult.Error("rename failed");
                break;
            case "entries":
                info.SetExists("/parent/child/child", false);
                info.PathEntryErrors.Add(dirPath);
                break;
            case "move":
                info.SetExists("/parent/child/child", false);
                io.MoveFileToResult = SimpleResult.Error("move failed");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
        }
    }

    private static FlattenInfoProviderMock BuildBaseInfoProvider()
    {
        FlattenInfoProviderMock info = new();
        info.SetExists("/parent/child/child", false);
        info.SetPathEntries(
            "/parent/child",
            dirs: [Dir("/parent/child/sub")],
            files: [File("/parent/child/a.txt")]
        );
        return info;
    }

    private static DirectoryEntryInfo Dir(string path) =>
        new(path, Path.GetFileName(path), DateTime.UnixEpoch.ToLocalTime(), DateTime.UnixEpoch);

    private static FileEntryInfo File(string path) =>
        new(
            path,
            Path.GetFileName(path),
            Path.GetExtension(path),
            1,
            DateTime.UnixEpoch.ToLocalTime(),
            DateTime.UnixEpoch
        );
}
