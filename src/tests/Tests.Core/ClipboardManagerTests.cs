using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.FileOperations.Undoable;
using Mocks;
using Mocks.Models;
using Mocks.Services;

namespace Tests.Core;

public class ClipboardManagerTests
{
    [Fact]
    public async Task CopyAsync_Remote_ClearsClipboard()
    {
        MockClipboardProxy proxy = new();
        MockFileSystem remoteFs = new() { Local = false };
        MockFileSystem localFs = new() { Local = true };
        ClipboardManager manager = new(proxy, localFs);
        IFileSystemEntry[] entries = [];
        Location loc = new(remoteFs, "/");

        await manager.CopyAsync(entries, loc);

        Assert.Contains(proxy.Calls, c => c.Method == nameof(MockClipboardProxy.ClearAsync));
    }

    [Fact]
    public async Task CutAsync_Local_SetsClipboard()
    {
        MockClipboardProxy proxy = new();
        MockFileSystem fs = new() { Local = true };
        ClipboardManager manager = new(proxy, fs);
        IFileSystemEntry[] entries = [];
        Location loc = new(fs, "/");

        await manager.CutAsync(entries, loc);

        Assert.Contains(
            proxy.Calls,
            c => c.Method == nameof(MockClipboardProxy.CopyToOsClipboardAsync)
        );
    }

    [Fact]
    public async Task PasteAsync_EmptyClipboard_ReturnsError()
    {
        MockClipboardProxy proxy = new();
        MockFileSystem fs = new();
        ClipboardManager manager = new(proxy, fs);
        Location dest = new(fs, "/dest");

        ValueResult<IUndoableFileOperation?> result = await manager.PasteAsync(
            dest,
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task PasteAsync_SameFsCopy_ReturnsCopyFilesTo()
    {
        MockClipboardProxy proxy = new();
        MockFileIoHandler ioHandler = new();
        MockFileSystem fs = new()
        {
            Local = true,
            FileIoHandler = ioHandler,
            FileInfoProvider = new MockFileInfoProvider(),
        };
        ClipboardManager manager = new(proxy, fs);
        FileEntry entry = new("/origin/test.txt", fs.FileInfoProvider.PathTools);
        IFileSystemEntry[] entries = [entry];
        Location originLoc = new(fs, "/origin");
        Location destLoc = new(fs, "/dest");

        await manager.CopyAsync(entries, originLoc);
        ValueResult<IUndoableFileOperation?> result = await manager.PasteAsync(
            destLoc,
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.True(result.IsOk);
        Assert.IsType<CopyFilesTo>(result.Value);
    }

    [Fact]
    public async Task PasteAsync_SameFsCopyToSameDir_ReturnsDuplicateFiles()
    {
        MockClipboardProxy proxy = new();
        MockFileIoHandler ioHandler = new();
        MockFileSystem fs = new()
        {
            Local = true,
            FileIoHandler = ioHandler,
            FileInfoProvider = new MockFileInfoProvider(),
        };
        ClipboardManager manager = new(proxy, fs);
        FileEntry entry = new("/same/test.txt", fs.FileInfoProvider.PathTools);
        IFileSystemEntry[] entries = [entry];
        Location sameLoc = new(fs, "/same");

        await manager.CopyAsync(entries, sameLoc);
        ValueResult<IUndoableFileOperation?> result = await manager.PasteAsync(
            sameLoc,
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.True(result.IsOk);
        Assert.IsType<DuplicateFiles>(result.Value);
    }

    [Fact]
    public async Task PasteAsync_SameFsCutToDifferentDir_ReturnsMoveFilesTo()
    {
        MockClipboardProxy proxy = new();
        MockFileIoHandler ioHandler = new();
        MockFileSystem fs = new()
        {
            Local = true,
            FileIoHandler = ioHandler,
            FileInfoProvider = new MockFileInfoProvider(),
        };
        ClipboardManager manager = new(proxy, fs);
        FileEntry entry = new("/origin/test.txt", fs.FileInfoProvider.PathTools);
        IFileSystemEntry[] entries = [entry];
        Location originLoc = new(fs, "/origin");
        Location destLoc = new(fs, "/dest");

        await manager.CutAsync(entries, originLoc);
        ValueResult<IUndoableFileOperation?> result = await manager.PasteAsync(
            destLoc,
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.True(result.IsOk);
        Assert.IsType<MoveFilesTo>(result.Value);
    }

    [Fact]
    public async Task PasteAsync_SameFsCutToSameDir_ReturnsError()
    {
        MockClipboardProxy proxy = new();
        MockFileIoHandler ioHandler = new();
        MockFileSystem fs = new()
        {
            Local = true,
            FileIoHandler = ioHandler,
            FileInfoProvider = new MockFileInfoProvider(),
        };
        ClipboardManager manager = new(proxy, fs);
        FileEntry entry = new("/same/test.txt", fs.FileInfoProvider.PathTools);
        IFileSystemEntry[] entries = [entry];
        Location sameLoc = new(fs, "/same");

        await manager.CutAsync(entries, sameLoc);
        ValueResult<IUndoableFileOperation?> result = await manager.PasteAsync(
            sameLoc,
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task PasteAsync_DifferentFsCopy_UploadFiles()
    {
        MockClipboardProxy proxy = new();
        MockFileIoHandler originIoHandler = new();
        MockFileIoHandler destIoHandler = new();
        MockFileSystem originFs = new()
        {
            Local = true,
            FileIoHandler = originIoHandler,
            FileInfoProvider = new MockFileInfoProvider(),
        };
        MockFileSystem destFs = new()
        {
            Local = true,
            FileIoHandler = destIoHandler,
            FileInfoProvider = new MockFileInfoProvider(),
        };
        ClipboardManager manager = new(proxy, originFs);
        FileEntry entry = new("/origin/test.txt", originFs.FileInfoProvider.PathTools);
        IFileSystemEntry[] entries = [entry];
        Location originLoc = new(originFs, "/origin");
        Location destLoc = new(destFs, "/dest");

        await manager.CopyAsync(entries, originLoc);
        ValueResult<IUndoableFileOperation?> result = await manager.PasteAsync(
            destLoc,
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.True(result.IsOk);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task PasteAsync_CutAfterSuccess_ClearsClipboard()
    {
        MockClipboardProxy proxy = new();
        MockFileIoHandler ioHandler = new();
        MockFileSystem fs = new()
        {
            Local = true,
            FileIoHandler = ioHandler,
            FileInfoProvider = new MockFileInfoProvider(),
        };
        ClipboardManager manager = new(proxy, fs);
        FileEntry entry = new("/origin/test.txt", fs.FileInfoProvider.PathTools);
        IFileSystemEntry[] entries = [entry];
        Location originLoc = new(fs, "/origin");
        Location destLoc = new(fs, "/dest");

        await manager.CutAsync(entries, originLoc);
        await manager.PasteAsync(destLoc, ProgressReporter.None, CancellationToken.None);
        int clearCalls = proxy.Calls.Count(c => c.Method == nameof(MockClipboardProxy.ClearAsync));

        Assert.True(clearCalls >= 1);
    }

    [Fact]
    public async Task PasteAsync_SecondPasteAfterCut_ReturnsError()
    {
        MockClipboardProxy proxy = new();
        MockFileIoHandler ioHandler = new();
        MockFileSystem fs = new()
        {
            Local = true,
            FileIoHandler = ioHandler,
            FileInfoProvider = new MockFileInfoProvider(),
        };
        ClipboardManager manager = new(proxy, fs);
        FileEntry entry = new("/origin/test.txt", fs.FileInfoProvider.PathTools);
        IFileSystemEntry[] entries = [entry];
        Location originLoc = new(fs, "/origin");
        Location destLoc = new(fs, "/dest");

        await manager.CutAsync(entries, originLoc);
        await manager.PasteAsync(destLoc, ProgressReporter.None, CancellationToken.None);

        ValueResult<IUndoableFileOperation?> result2 = await manager.PasteAsync(
            destLoc,
            ProgressReporter.None,
            CancellationToken.None
        );

        Assert.False(result2.IsOk);
    }
}
