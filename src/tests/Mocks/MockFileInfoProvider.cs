using System.Diagnostics.CodeAnalysis;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace Mocks;

public class MockFileInfoProvider : IFileInfoProvider
{
    public virtual IPathTools PathTools => null!;

    public virtual bool IsLinkedToDirectory(
        string linkPath,
        [NotNullWhen(true)] out string? directory
    ) => throw new NotImplementedException();

    public virtual ValueResult<DirectoryContents> GetPathEntries(
        string path,
        bool includeHidden,
        bool includeOs
    ) => throw new NotImplementedException();

    public virtual Task<ValueResult<DirectoryContents>> GetPathEntriesAsync(
        string path,
        bool includeHidden,
        bool includeOs,
        CancellationToken ct
    ) => throw new NotImplementedException();

    public virtual ValueResult<Stream> GetFileStream(string path) =>
        throw new NotImplementedException();

    public virtual DateTime? GetFileLastWriteUtc(string filePath) =>
        throw new NotImplementedException();

    public virtual Task<DateTime?> GetFileLastWriteUtcAsync(string filePath) =>
        throw new NotImplementedException();

    public virtual DateTime? GetDirLastWriteUtc(string dirPath) =>
        throw new NotImplementedException();

    public virtual Task<DateTime?> GetDirLastWriteUtcAsync(string dirPath) =>
        throw new NotImplementedException();

    public virtual bool IsHidden(string path, bool isDirectory) =>
        throw new NotImplementedException();

    public virtual string GetRoot() => throw new NotImplementedException();

    public virtual ExistsInfo Exists(string path) => throw new NotImplementedException();

    public virtual Task<ExistsInfo> ExistsAsync(string path) => throw new NotImplementedException();
}
