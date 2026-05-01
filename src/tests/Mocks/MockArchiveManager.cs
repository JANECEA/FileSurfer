using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;

namespace Mocks;

public class MockArchiveManager : ServiceMock, IArchiveManager
{
    public virtual bool IsArchived(string filePath)
    {
        RecordCall(nameof(IsArchived), filePath);
        return false;
    }

    public virtual Task<IResult> ArchiveEntriesAsync(
        IList<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        RecordCall(nameof(ArchiveEntriesAsync), entries, destinationDir, archiveName, reporter, ct);
        return Task.FromResult<IResult>(SimpleResult.Ok());
    }

    public virtual Task<IResult> ExtractArchiveAsync(
        string archivePath,
        string destinationPath,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        RecordCall(nameof(ExtractArchiveAsync), archivePath, destinationPath, reporter, ct);
        return Task.FromResult<IResult>(SimpleResult.Ok());
    }
}
