using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;

namespace FileSurfer.Core.Services.FileOperations;

public interface IArchiveManager
{
    /// <summary>
    /// Determines if the file is an archive in the context of <see cref="FileSurfer"/>.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns><see langword="true"/> if the file has one of the supported extensions, otherwise <see langword="false"/>.</returns>
    bool IsZipped(string filePath);

    /// <summary>
    /// Compresses specified file paths into a new archive.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    Task<IResult> ZipFiles(
        IList<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName,
        CancellationToken ct
    );

    /// <summary>
    /// Extracts the archive, overwriting already existing files.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    Task<IResult> UnzipArchive(string archivePath, string destinationPath, CancellationToken ct);
}
