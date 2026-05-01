using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations.Undoable;
using Mocks;
using Mocks.Models;
using Mocks.Services;

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
    private static readonly string Root =
        Path.GetPathRoot(Path.GetFullPath($"{LocalPathTools.DirSeparator}"))
        ?? $"{LocalPathTools.DirSeparator}";

    private static string Abs(params string[] parts)
    {
        string path = Root;
        foreach (string part in parts)
            path = LocalPathTools.Combine(path, part);

        return path;
    }

    [Fact]
    public async Task InvokeAsync_ReturnsError_ForTopLevelDirectory()
    {
        FlattenFileIoMock io = new();
        FlattenInfoProviderMock info = new();
        FlattenFolder operation = new(io, info, Root);

        IResult result = await operation.InvokeAsync(ProgressReporter.None, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    public static TheoryData<string> InvokeFailureStages => ["rename", "entries", "move"];

    [Theory]
    [MemberData(nameof(InvokeFailureStages))]
    public async Task InvokeAsync_StopsOnExpectedFailureStage(string stage)
    {
        string dirPath = Abs("parent", "child");
        FlattenFileIoMock io = new();
        FlattenInfoProviderMock info = BuildBaseInfoProvider(dirPath);
        ConfigureInvokeFailure(stage, io, info, dirPath);
        FlattenFolder operation = new(io, info, dirPath);

        IResult result = await operation.InvokeAsync(ProgressReporter.None, CancellationToken.None);

        Assert.False(result.IsOk);
        Assert.DoesNotContain(io.Calls, call => call.Method == nameof(FlattenFileIoMock.DeleteDir));
    }

    [Fact]
    public async Task InvokeAsync_MovesEntriesAndDeletesSource_WhenSuccessful()
    {
        string parentDir = Abs("parent");
        string dirPath = Abs("parent", "child");
        FlattenFileIoMock io = new();
        FlattenInfoProviderMock info = BuildBaseInfoProvider(dirPath);
        FlattenFolder operation = new(io, info, dirPath);

        IResult result = await operation.InvokeAsync(ProgressReporter.None, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.Contains(
            io.Calls.Where(call => call.Method == nameof(FlattenFileIoMock.MoveFileTo)),
            call => (string)call.Args[0] == Abs("parent", "child", "a.txt") && (string)call.Args[1] == parentDir
        );
        Assert.Contains(
            io.Calls.Where(call => call.Method == nameof(FlattenFileIoMock.MoveDirTo)),
            call => (string)call.Args[0] == Abs("parent", "child", "sub") && (string)call.Args[1] == parentDir
        );
        MethodCall deleteCall = Assert.Single(
            io.Calls,
            call => call.Method == nameof(FlattenFileIoMock.DeleteDir)
        );
        Assert.Equal(dirPath, (string)deleteCall.Args[0]);
    }

    [Fact]
    public async Task UndoAsync_ReturnsError_WhenOriginalPathExistsAndNameNotInMovedEntries()
    {
        string dirPath = Abs("parent", "child");
        FlattenFileIoMock io = new();
        FlattenInfoProviderMock info = BuildBaseInfoProvider(dirPath);
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
        string parentDir = Abs("parent");
        string originalDirPath = Abs("parent", "child");
        string renamedDirPath = Abs("parent", "child (1)");
        string nestedOriginalDirPath = Abs("parent", "child", "child");
        string nestedRenamedDirPath = Abs("parent", "child (1)", "child");

        FlattenFileIoMock io = new();
        FlattenInfoProviderMock info = new();
        info.SetExists(nestedOriginalDirPath, true);
        info.SetExists(originalDirPath, true);
        info.SetExists(renamedDirPath, false);
        info.SetPathEntries(
            renamedDirPath,
            dirs: [Dir(nestedRenamedDirPath)],
            files: [File(Abs("parent", "child (1)", "a.txt"))]
        );

        FlattenFolder operation = new(io, info, originalDirPath);
        IResult invokeResult = await operation.InvokeAsync(
            ProgressReporter.None,
            CancellationToken.None
        );
        Assert.True(invokeResult.IsOk);

        info.SetExists(originalDirPath, true);
        info.SetExists(renamedDirPath, false);

        IResult undoResult = await operation.UndoAsync(
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.True(undoResult.IsOk);
        Assert.Contains(
            io.Calls.Where(call => call.Method == nameof(FlattenFileIoMock.NewDirAt)),
            call => (string)call.Args[0] == parentDir && (string)call.Args[1] == "child (1)"
        );
        Assert.Contains(
            io.Calls.Where(call => call.Method == nameof(FlattenFileIoMock.MoveDirTo)),
            call => (string)call.Args[0] == originalDirPath && (string)call.Args[1] == renamedDirPath
        );
        Assert.Contains(
            io.Calls.Where(call => call.Method == nameof(FlattenFileIoMock.MoveFileTo)),
            call => (string)call.Args[0] == Abs("parent", "a.txt") && (string)call.Args[1] == renamedDirPath
        );
        Assert.Contains(
            io.Calls.Where(call => call.Method == nameof(FlattenFileIoMock.RenameDirAt)),
            call => (string)call.Args[0] == renamedDirPath && (string)call.Args[1] == "child"
        );
    }

    private static void ConfigureInvokeFailure(
        string stage,
        FlattenFileIoMock io,
        FlattenInfoProviderMock info,
        string dirPath
    )
    {
        string dirName = LocalPathTools.GetFileName(dirPath);
        string parentDir = LocalPathTools.GetParentDir(dirPath);
        string nestedDirPath = LocalPathTools.Combine(dirPath, dirName);
        string renamedDirPath = LocalPathTools.Combine(parentDir, $"{dirName} (1)");

        switch (stage)
        {
            case "rename":
                info.SetExists(nestedDirPath, true);
                info.SetExists(dirPath, true);
                info.SetExists(renamedDirPath, false);
                io.RenameDirAtResult = SimpleResult.Error("rename failed");
                break;
            case "entries":
                info.SetExists(nestedDirPath, false);
                info.PathEntryErrors.Add(dirPath);
                break;
            case "move":
                info.SetExists(nestedDirPath, false);
                io.MoveFileToResult = SimpleResult.Error("move failed");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
        }
    }

    private static FlattenInfoProviderMock BuildBaseInfoProvider(string dirPath)
    {
        string dirName = LocalPathTools.GetFileName(dirPath);
        FlattenInfoProviderMock info = new();
        info.SetExists(LocalPathTools.Combine(dirPath, dirName), false);
        info.SetPathEntries(
            dirPath,
            dirs: [Dir(LocalPathTools.Combine(dirPath, "sub"))],
            files: [File(LocalPathTools.Combine(dirPath, "a.txt"))]
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
