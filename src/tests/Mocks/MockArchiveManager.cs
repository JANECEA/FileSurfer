using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;

namespace Mocks;

public class MockArchiveManager : IArchiveManager
{
    public bool IsArchived(string filePath) => throw new NotImplementedException();

    public Task<IResult> ArchiveEntriesAsync(
        IList<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName,
        ProgressReporter reporter,
        CancellationToken ct
    ) => throw new NotImplementedException();

    public Task<IResult> ExtractArchiveAsync(
        string archivePath,
        string destinationPath,
        ProgressReporter reporter,
        CancellationToken ct
    ) => throw new NotImplementedException();
}
