using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using Mocks;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Tests.Core;

internal sealed class TrackingStream : MemoryStream
{
    public bool IsDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}

internal sealed class TransferStreamFileInfoProviderMock : MockFileInfoProvider
{
    private Dictionary<string, DirectoryContents> EntryMap { get; } = [];
    public Dictionary<string, TrackingStream> Streams { get; } = [];
    public HashSet<string> EntryErrorPaths { get; } = [];
    public HashSet<string> StreamErrorPaths { get; } = [];
    public List<(string Path, bool IncludeHidden, bool IncludeOs)> PathEntryRequests { get; } = [];
    public List<string> OpenedStreamPaths { get; } = [];

    public override ValueResult<DirectoryContents> GetPathEntries(
        string path,
        bool includeHidden,
        bool includeOs
    )
    {
        RecordCall(nameof(GetPathEntries), path, includeHidden, includeOs);
        PathEntryRequests.Add((path, includeHidden, includeOs));

        if (EntryErrorPaths.Contains(path))
            return ValueResult<DirectoryContents>.Error("Configured directory listing failure.");

        return EntryMap.TryGetValue(path, out DirectoryContents? contents)
            ? ValueResult<DirectoryContents>.Ok(contents)
            : ValueResult<DirectoryContents>.Error("Path was not configured.");
    }

    public override ValueResult<Stream> GetFileStream(string path)
    {
        RecordCall(nameof(GetFileStream), path);

        if (StreamErrorPaths.Contains(path))
            return ValueResult<Stream>.Error("Configured file stream failure.");

        if (!Streams.TryGetValue(path, out TrackingStream? stream))
            return ValueResult<Stream>.Error("Stream path was not configured.");

        OpenedStreamPaths.Add(path);
        return ValueResult<Stream>.Ok(stream);
    }

    public static TransferStreamFileInfoProviderMock BuildBaseProvider()
    {
        TransferStreamFileInfoProviderMock provider = new();
        DateTime t = DateTime.UnixEpoch;

        provider.EntryMap["/root"] = new DirectoryContents
        {
            Dirs = [MockHelper.Dir("/root/a", t)],
            Files = [MockHelper.File("/root/r1.txt", 10, t)],
        };
        provider.EntryMap["/root/a"] = new DirectoryContents
        {
            Dirs = [MockHelper.Dir("/root/a/b", t)],
            Files = [MockHelper.File("/root/a/a1.txt", 11, t)],
        };
        provider.EntryMap["/root/a/b"] = new DirectoryContents
        {
            Dirs = [],
            Files = [MockHelper.File("/root/a/b/b1.txt", 12, t)],
        };

        provider.Streams["/root/r1.txt"] = new TrackingStream();
        provider.Streams["/root/a/a1.txt"] = new TrackingStream();
        provider.Streams["/root/a/b/b1.txt"] = new TrackingStream();
        return provider;
    }

    public static TransferStreamFileInfoProviderMock BuildGeneratedProvider(
        int depth,
        int filesPerDirectory
    )
    {
        TransferStreamFileInfoProviderMock provider = new();
        DateTime t = DateTime.UnixEpoch;
        string currentPath = "/root";

        for (int level = 0; level < depth; level++)
        {
            string? nextPath = level < depth - 1 ? $"{currentPath}/d{level + 1}" : null;

            List<FileEntryInfo> files = [];
            for (int i = 0; i < filesPerDirectory; i++)
            {
                string filePath = $"{currentPath}/f{level}_{i}.txt";
                files.Add(MockHelper.File(filePath, 100 + i, t));
                provider.Streams[filePath] = new TrackingStream();
            }

            provider.EntryMap[currentPath] = new DirectoryContents
            {
                Dirs = nextPath is null ? [] : [MockHelper.Dir(nextPath, t)],
                Files = files,
            };

            if (nextPath is not null)
                currentPath = nextPath;
        }

        return provider;
    }
}

public class TransferStreamTests
{
    [Fact]
    public void DirTransferStream_FromInfoProvider_BuildsTreeFromProvider()
    {
        const string root = "/root";
        TransferStreamFileInfoProviderMock provider =
            TransferStreamFileInfoProviderMock.BuildBaseProvider();

        ValueResult<DirTransferStream> result = DirTransferStream.FromInfoProvider(
            provider,
            root,
            includeHidden: false,
            includeOs: false
        );

        Assert.True(result.IsOk);
        using DirTransferStream transfer = result.Value;
        Assert.Equal("root", transfer.Name);
        Assert.Single(transfer.Files);
        Assert.Equal("r1.txt", transfer.Files[0].Name);
        Assert.Single(transfer.Directories);
        Assert.Equal("a", transfer.Directories[0].Name);
        Assert.Single(transfer.Directories[0].Files);
        Assert.Equal("a1.txt", transfer.Directories[0].Files[0].Name);
        Assert.Single(transfer.Directories[0].Directories);
        Assert.Equal("b", transfer.Directories[0].Directories[0].Name);
        Assert.Single(transfer.Directories[0].Directories[0].Files);
        Assert.Equal("b1.txt", transfer.Directories[0].Directories[0].Files[0].Name);

        Assert.Collection(
            provider.PathEntryRequests,
            req =>
            {
                Assert.Equal("/root", req.Path);
                Assert.False(req.IncludeHidden);
                Assert.False(req.IncludeOs);
            },
            req =>
            {
                Assert.Equal("/root/a", req.Path);
                Assert.False(req.IncludeHidden);
                Assert.False(req.IncludeOs);
            },
            req =>
            {
                Assert.Equal("/root/a/b", req.Path);
                Assert.False(req.IncludeHidden);
                Assert.False(req.IncludeOs);
            }
        );
    }

    public static TheoryData<string, int> BuildFailureCases =>
        new()
        {
            { "entries:/root", 0 },
            { "entries:/root/a", 1 },
            { "stream:/root/r1.txt", 0 },
            { "stream:/root/a/a1.txt", 1 },
        };

    [Theory]
    [MemberData(nameof(BuildFailureCases))]
    public void DirTransferStream_FromInfoProvider_DisposesOpenedStreams_WhenBuildFails(
        string failureCase,
        int expectedOpenedStreams
    )
    {
        var provider = TransferStreamFileInfoProviderMock.BuildBaseProvider();
        ConfigureFailure(provider, failureCase);

        ValueResult<DirTransferStream> result = DirTransferStream.FromInfoProvider(
            provider,
            "/root",
            includeHidden: true,
            includeOs: true
        );

        Assert.False(result.IsOk);
        Assert.Equal(expectedOpenedStreams, provider.OpenedStreamPaths.Count);
        Assert.All(
            provider.OpenedStreamPaths,
            path => Assert.True(provider.Streams[path].IsDisposed)
        );
    }

    public static TheoryData<int, int> DisposeShapeCases =>
        new()
        {
            { 1, 1 },
            { 2, 2 },
            { 3, 1 },
        };

    [Theory]
    [MemberData(nameof(DisposeShapeCases))]
    public void DirTransferStream_Dispose_DisposesAllNestedFileStreams(
        int depth,
        int filesPerDirectory
    )
    {
        const string root = "/root";
        var provider = TransferStreamFileInfoProviderMock.BuildGeneratedProvider(
            depth,
            filesPerDirectory
        );
        int expectedStreamCount = provider.Streams.Count;

        ValueResult<DirTransferStream> result = DirTransferStream.FromInfoProvider(
            provider,
            root,
            includeHidden: false,
            includeOs: false
        );

        Assert.True(result.IsOk);
        DirTransferStream transfer = result.Value;
        Assert.All(provider.Streams.Values, stream => Assert.False(stream.IsDisposed));

        transfer.Dispose();

        Assert.Equal(expectedStreamCount, provider.OpenedStreamPaths.Count);
        Assert.All(provider.Streams.Values, stream => Assert.True(stream.IsDisposed));
    }

    private static void ConfigureFailure(
        TransferStreamFileInfoProviderMock provider,
        string failureCase
    )
    {
        string[] split = failureCase.Split(':', 2);
        string type = split[0];
        string path = split[1];

        if (type == "entries")
            provider.EntryErrorPaths.Add(path);
        else
            provider.StreamErrorPaths.Add(path);
    }
}
