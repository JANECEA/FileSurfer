using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;

namespace FileSurfer.Core.Services.FileOperations;

/// <summary>
/// Defines archive-related operations used to detect archive files, create new archives from entries,
/// and extract archive contents to a destination path.
/// </summary>
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
    /// <param name="entries">
    /// The files and directories to include in the archive.
    /// </param>
    /// <param name="destinationDir">
    /// The destination directory where the archive should be created.
    /// </param>
    /// <param name="archiveName">
    /// The base name to use for the created archive file.
    /// </param>
    /// <param name="reporter">
    /// Progress reporter used to publish archive creation progress.
    /// </param>
    /// <param name="ct">
    /// Cancellation token used to stop the archive operation.
    /// </param>
    /// <returns>
    /// A task that returns the operation result, including any error details if archive creation fails.
    /// </returns>
    Task<IResult> ArchiveEntriesAsync(
        IList<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName,
        ProgressReporter reporter,
        CancellationToken ct
    );

    /// <summary>
    /// Extracts the archive, overwriting already existing files.
    /// </summary>
    /// <param name="archivePath">
    /// The full path to the archive file to extract.
    /// </param>
    /// <param name="destinationPath">
    /// The destination directory where archive contents should be extracted.
    /// </param>
    /// <param name="reporter">
    /// Progress reporter used to publish extraction progress.
    /// </param>
    /// <param name="ct">
    /// Cancellation token used to stop the extraction operation.
    /// </param>
    /// <returns>
    /// A task that returns the operation result, including any error details if extraction fails.
    /// </returns>
    Task<IResult> ExtractArchiveAsync(
        string archivePath,
        string destinationPath,
        ProgressReporter reporter,
        CancellationToken ct
    );
}
