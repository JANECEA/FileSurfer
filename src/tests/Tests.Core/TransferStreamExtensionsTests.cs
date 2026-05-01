using System.Text;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using Mocks;

namespace Tests.Core;

internal sealed class ThrowingWriteStream : MemoryStream
{
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new IOException("Configured write failure.");

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => Task.FromException(new IOException("Configured write failure."));
}

internal sealed class TransferIoHandlerMock : MockFileIoHandler
{
    public List<(string ParentPath, string Name)> CreatedDirs { get; } = [];
    public List<(string ParentPath, string Name)> WrittenFiles { get; } = [];
    public int? FailNewDirAtCallIndex { get; init; }
    public string? FailWriteFileName { get; init; }

    private int _newDirCallCount;

    public override IResult NewDirAt(string dirPath, string dirName)
    {
        RecordCall(nameof(NewDirAt), dirPath, dirName);
        CreatedDirs.Add((dirPath, dirName));
        _newDirCallCount++;

        return FailNewDirAtCallIndex == _newDirCallCount
            ? SimpleResult.Error("Configured directory creation failure.")
            : SimpleResult.Ok();
    }

    public override Task<IResult> WriteFileStreamAsync(
        FileTransferStream fileStream,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        RecordCall(nameof(WriteFileStreamAsync), fileStream, dirPath, reporter, ct);
        WrittenFiles.Add((dirPath, fileStream.Name));

        IResult result =
            fileStream.Name == FailWriteFileName
                ? SimpleResult.Error("Configured file write failure.")
                : SimpleResult.Ok();
        return Task.FromResult(result);
    }
}

public class TransferStreamExtensionsTests
{
    [Fact]
    public async Task WriteToStreamAsync_CopiesContent_WhenSuccessful()
    {
        using MemoryStream source = new("hello"u8.ToArray());
        FileTransferStream fileStream = new("a.txt", source);
        using MemoryStream target = new();

        IResult result = await fileStream.WriteToStreamAsync(
            target,
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.True(result.IsOk);
        Assert.Equal("hello", Encoding.UTF8.GetString(target.ToArray()));
    }

    [Fact]
    public async Task WriteToStreamAsync_ReturnsCanceledError_WhenTokenCanceled()
    {
        using MemoryStream source = new([1, 2, 3]);
        FileTransferStream fileStream = new("a.txt", source);
        using MemoryStream target = new();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        IResult result = await fileStream.WriteToStreamAsync(
            target,
            ProgressReporter.None,
            cts.Token
        );

        Assert.False(result.IsOk);
        Assert.Equal("File transfer has been canceled.", result.Errors.First());
    }

    [Fact]
    public async Task WriteToStreamAsync_ReturnsUnderlyingError_WhenDestinationWriteFails()
    {
        using MemoryStream source = new([1, 2, 3]);
        FileTransferStream fileStream = new("a.txt", source);
        await using ThrowingWriteStream target = new();

        IResult result = await fileStream.WriteToStreamAsync(
            target,
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.False(result.IsOk);
        Assert.Equal("Configured write failure.", result.Errors.First());
    }

    [Fact]
    public async Task WriteWithIoHandlerAsync_CreatesDirsAndWritesFiles_WhenSuccessful()
    {
        TransferStreamFileInfoProviderMock provider =
            TransferStreamFileInfoProviderMock.BuildBaseProvider();
        ValueResult<DirTransferStream> streamResult = DirTransferStream.FromInfoProvider(
            provider,
            "/root",
            includeHidden: false,
            includeOs: false
        );
        Assert.True(streamResult.IsOk);
        using DirTransferStream dirStream = streamResult.Value;
        TransferIoHandlerMock io = new();
        IPathTools pathTools = LocalPathTools.Instance;

        IResult result = await dirStream.WriteWithIoHandlerAsync(
            io,
            pathTools,
            "/target",
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.True(result.IsOk);
        Assert.Equal(
            [
                ("/target", "root"),
                (pathTools.Combine("/target", "root"), "a"),
                (pathTools.Combine(pathTools.Combine("/target", "root"), "a"), "b"),
            ],
            io.CreatedDirs
        );
        Assert.Equal(
            [
                (pathTools.Combine("/target", "root"), "r1.txt"),
                (pathTools.Combine(pathTools.Combine("/target", "root"), "a"), "a1.txt"),
                (
                    pathTools.Combine(
                        pathTools.Combine(pathTools.Combine("/target", "root"), "a"),
                        "b"
                    ),
                    "b1.txt"
                ),
            ],
            io.WrittenFiles
        );
    }

    [Fact]
    public async Task WriteWithIoHandlerAsync_DeletesCreatedRootDir_WhenWriteFailsAfterRootCreation()
    {
        TransferStreamFileInfoProviderMock provider =
            TransferStreamFileInfoProviderMock.BuildBaseProvider();
        ValueResult<DirTransferStream> streamResult = DirTransferStream.FromInfoProvider(
            provider,
            "/root",
            includeHidden: false,
            includeOs: false
        );
        Assert.True(streamResult.IsOk);
        using DirTransferStream dirStream = streamResult.Value;
        TransferIoHandlerMock io = new() { FailWriteFileName = "a1.txt" };
        IPathTools pathTools = LocalPathTools.Instance;

        IResult result = await dirStream.WriteWithIoHandlerAsync(
            io,
            pathTools,
            "/target",
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.False(result.IsOk);
        MethodCall deleteCall = Assert.Single(
            io.Calls,
            call => call.Method == nameof(TransferIoHandlerMock.DeleteDir)
        );
        Assert.Equal(pathTools.Combine("/target", "root"), (string)deleteCall.Args[0]);
    }

    [Fact]
    public async Task WriteWithIoHandlerAsync_DoesNotDeleteRootDir_WhenInitialCreateFails()
    {
        TransferStreamFileInfoProviderMock provider =
            TransferStreamFileInfoProviderMock.BuildBaseProvider();
        ValueResult<DirTransferStream> streamResult = DirTransferStream.FromInfoProvider(
            provider,
            "/root",
            includeHidden: false,
            includeOs: false
        );
        Assert.True(streamResult.IsOk);
        using DirTransferStream dirStream = streamResult.Value;
        TransferIoHandlerMock io = new() { FailNewDirAtCallIndex = 1 };

        IResult result = await dirStream.WriteWithIoHandlerAsync(
            io,
            LocalPathTools.Instance,
            "/target",
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.False(result.IsOk);
        Assert.DoesNotContain(
            io.Calls,
            call => call.Method == nameof(TransferIoHandlerMock.DeleteDir)
        );
    }
}
