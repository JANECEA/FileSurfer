using System.Diagnostics.CodeAnalysis;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace Mocks;

public class MockFileInfoProvider : ServiceMock, IFileInfoProvider
{
    public virtual IPathTools PathTools
    {
        get
        {
            RecordCall(nameof(PathTools));
            return LocalPathTools.Instance;
        }
    }

    public virtual bool IsLinkedToDirectory(
        string linkPath,
        [NotNullWhen(true)] out string? directory
    )
    {
        directory = null;
        RecordCall(nameof(IsLinkedToDirectory), linkPath);
        return false;
    }

    public virtual ValueResult<DirectoryContents> GetPathEntries(
        string path,
        bool includeHidden,
        bool includeOs
    )
    {
        RecordCall(nameof(GetPathEntries), path, includeHidden, includeOs);
        return ValueResult<DirectoryContents>.Ok(new DirectoryContents { Dirs = [], Files = [] });
    }

    public virtual Task<ValueResult<DirectoryContents>> GetPathEntriesAsync(
        string path,
        bool includeHidden,
        bool includeOs,
        CancellationToken ct
    )
    {
        RecordCall(nameof(GetPathEntriesAsync), path, includeHidden, includeOs, ct);
        return Task.FromResult(GetPathEntries(path, includeHidden, includeOs));
    }

    public virtual ValueResult<Stream> GetFileStream(string path)
    {
        RecordCall(nameof(GetFileStream), path);
        return ValueResult<Stream>.Ok(Stream.Null);
    }

    public virtual DateTime? GetFileLastWriteUtc(string filePath)
    {
        RecordCall(nameof(GetFileLastWriteUtc), filePath);
        return DateTime.UnixEpoch;
    }

    public virtual Task<DateTime?> GetFileLastWriteUtcAsync(string filePath)
    {
        RecordCall(nameof(GetFileLastWriteUtcAsync), filePath);
        return Task.FromResult<DateTime?>(DateTime.UnixEpoch);
    }

    public virtual DateTime? GetDirLastWriteUtc(string dirPath)
    {
        RecordCall(nameof(GetDirLastWriteUtc), dirPath);
        return DateTime.UnixEpoch;
    }

    public virtual Task<DateTime?> GetDirLastWriteUtcAsync(string dirPath)
    {
        RecordCall(nameof(GetDirLastWriteUtcAsync), dirPath);
        return Task.FromResult<DateTime?>(DateTime.UnixEpoch);
    }

    public virtual bool IsHidden(string path, bool isDirectory)
    {
        RecordCall(nameof(IsHidden), path, isDirectory);
        return false;
    }

    public virtual string GetRoot()
    {
        RecordCall(nameof(GetRoot));
        return new string(LocalPathTools.DirSeparator, 1);
    }

    public virtual ExistsInfo Exists(string path)
    {
        RecordCall(nameof(Exists), path);
        return ExistsInfo.DoesNotExist();
    }

    public virtual Task<ExistsInfo> ExistsAsync(string path)
    {
        RecordCall(nameof(ExistsAsync), path);
        return Task.FromResult(Exists(path));
    }
}
