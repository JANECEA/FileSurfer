using System.Diagnostics.CodeAnalysis;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace Mocks;

public class MockFileInfoProvider : IFileInfoProvider
{
    public IPathTools PathTools => null!;

    public bool IsLinkedToDirectory(string linkPath, [NotNullWhen(true)] out string? directory) =>
        throw new NotImplementedException();

    public ValueResult<DirectoryContents> GetPathEntries(
        string path,
        bool includeHidden,
        bool includeOs
    ) => throw new NotImplementedException();

    public Task<ValueResult<DirectoryContents>> GetPathEntriesAsync(
        string path,
        bool includeHidden,
        bool includeOs,
        CancellationToken ct
    ) => throw new NotImplementedException();

    public ValueResult<Stream> GetFileStream(string path) => throw new NotImplementedException();

    public DateTime? GetFileLastWriteUtc(string filePath) => throw new NotImplementedException();

    public Task<DateTime?> GetFileLastWriteUtcAsync(string filePath) =>
        throw new NotImplementedException();

    public DateTime? GetDirLastWriteUtc(string dirPath) => throw new NotImplementedException();

    public Task<DateTime?> GetDirLastWriteUtcAsync(string dirPath) =>
        throw new NotImplementedException();

    public bool IsHidden(string path, bool isDirectory) => throw new NotImplementedException();

    public string GetRoot() => throw new NotImplementedException();

    public ExistsInfo Exists(string path) => throw new NotImplementedException();

    public Task<ExistsInfo> ExistsAsync(string path) => throw new NotImplementedException();
}
