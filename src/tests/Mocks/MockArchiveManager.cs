using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;

namespace Mocks;

public class MockArchiveManager : IArchiveManager
{
    public virtual bool IsArchived(string filePath) => throw new NotImplementedException();

    public virtual Task<IResult> ArchiveEntriesAsync(
        IList<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName,
        ProgressReporter reporter,
        CancellationToken ct
    ) => throw new NotImplementedException();

    public virtual Task<IResult> ExtractArchiveAsync(
        string archivePath,
        string destinationPath,
        ProgressReporter reporter,
        CancellationToken ct
    ) => throw new NotImplementedException();
}
