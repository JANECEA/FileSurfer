using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;

namespace FileSurfer.Core.Services.FileOperations;

public interface IArchiveManager
{
    /// <summary>
    /// Determines if the file is an archive.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns><see langword="true"/> if the file has one of the supported extensions, otherwise <see langword="false"/>.</returns>
    bool IsArchived(string filePath);

    /// <summary>
    /// Compresses specified file paths into a new archive.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    Task<IResult> ArchiveEntries(
        IList<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName,
        ProgressReporter reporter,
        CancellationToken ct
    );

    /// <summary>
    /// Extracts the archive, overwriting already existing files.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    Task<IResult> ExtractArchive(
        string archivePath,
        string destinationPath,
        ProgressReporter reporter,
        CancellationToken ct
    );
}
