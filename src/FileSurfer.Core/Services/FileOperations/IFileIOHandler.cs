using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;

namespace FileSurfer.Core.Services.FileOperations;

/// <summary>
/// Defines file and directory I/O operations used by local and remote file-system implementations.
/// </summary>
public interface IFileIoHandler
{
    /// <summary>
    /// Creates a new file at the specified directory path.
    /// </summary>
    /// <param name="dirPath">
    /// The target directory where the file should be created.
    /// </param>
    /// <param name="fileName">
    /// The name of the file to create.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if file creation fails.
    /// </returns>
    public IResult NewFileAt(string dirPath, string fileName);

    /// <summary>
    /// Creates a new directory at the specified path.
    /// </summary>
    /// <param name="dirPath">
    /// The parent directory where the new directory should be created.
    /// </param>
    /// <param name="dirName">
    /// The name of the directory to create.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if directory creation fails.
    /// </returns>
    public IResult NewDirAt(string dirPath, string dirName);

    /// <summary>
    /// Renames a file at the specified path.
    /// </summary>
    /// <param name="filePath">
    /// The full path of the file to rename.
    /// </param>
    /// <param name="newName">
    /// The new file name.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if rename fails.
    /// </returns>
    public IResult RenameFileAt(string filePath, string newName);

    /// <summary>
    /// Renames a directory at the specified path.
    /// </summary>
    /// <param name="dirPath">
    /// The full path of the directory to rename.
    /// </param>
    /// <param name="newName">
    /// The new directory name.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if rename fails.
    /// </returns>
    public IResult RenameDirAt(string dirPath, string newName);

    /// <summary>
    /// Moves a file to a specified destination directory.
    /// </summary>
    /// <param name="filePath">
    /// The full path of the file to move.
    /// </param>
    /// <param name="destinationDir">
    /// The destination directory path.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if move fails.
    /// </returns>
    public IResult MoveFileTo(string filePath, string destinationDir);

    /// <summary>
    /// Moves a directory to a specified destination directory.
    /// </summary>
    /// <param name="dirPath">
    /// The full path of the directory to move.
    /// </param>
    /// <param name="destinationDir">
    /// The destination directory path.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if move fails.
    /// </returns>
    public IResult MoveDirTo(string dirPath, string destinationDir);

    /// <summary>
    /// Copies a file to a specified destination directory.
    /// </summary>
    /// <param name="filePath">
    /// The full path of the file to copy.
    /// </param>
    /// <param name="destinationDir">
    /// The destination directory path.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if copy fails.
    /// </returns>
    public IResult CopyFileTo(string filePath, string destinationDir);

    /// <summary>
    /// Copies a directory to a specified destination directory.
    /// </summary>
    /// <param name="dirPath">
    /// The full path of the directory to copy.
    /// </param>
    /// <param name="destinationDir">
    /// The destination directory path.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if copy fails.
    /// </returns>
    public IResult CopyDirTo(string dirPath, string destinationDir);

    /// <summary>
    /// Creates a duplicate of a file with a new name.
    /// </summary>
    /// <param name="filePath">
    /// The full path of the file to duplicate.
    /// </param>
    /// <param name="copyName">
    /// The name for the duplicated file.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if duplication fails.
    /// </returns>
    public IResult DuplicateFile(string filePath, string copyName);

    /// <summary>
    /// Creates a duplicate of a directory with a new name.
    /// </summary>
    /// <param name="dirPath">
    /// The full path of the directory to duplicate.
    /// </param>
    /// <param name="copyName">
    /// The name for the duplicated directory.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if duplication fails.
    /// </returns>
    public IResult DuplicateDir(string dirPath, string copyName);

    /// <summary>
    /// Deletes a file permanently.
    /// </summary>
    /// <param name="filePath">
    /// The full path of the file to delete.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if deletion fails.
    /// </returns>
    public IResult DeleteFile(string filePath);

    /// <summary>
    /// Deletes a directory permanently.
    /// </summary>
    /// <param name="dirPath">
    /// The full path of the directory to delete.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if deletion fails.
    /// </returns>
    public IResult DeleteDir(string dirPath);

    /// <summary>
    /// Writes the filestream into a file with name specified in <see cref="FileTransferStream.Name"/>.
    /// </summary>
    /// <param name="fileStream">
    /// The file transfer stream containing content and metadata for the file to write.
    /// </param>
    /// <param name="dirPath">
    /// Destination directory where the file should be written.
    /// </param>
    /// <param name="reporter">
    /// Progress reporter used to emit transfer progress.
    /// </param>
    /// <param name="ct">
    /// Cancellation token used to stop the write operation.
    /// </param>
    /// <returns>
    /// A task that returns the operation result, including any error details if writing fails.
    /// </returns>
    public Task<IResult> WriteFileStreamAsync(
        FileTransferStream fileStream,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    );

    /// <summary>
    /// Writes the all containing file streams and directories into a file with names specified in
    /// <see cref="FileTransferStream.Name"/> and <see cref="DirTransferStream.Name"/>.
    /// </summary>
    /// <param name="dirStream">
    /// The directory transfer stream containing nested files and directories to write.
    /// </param>
    /// <param name="dirPath">
    /// Destination root directory where the streamed directory structure should be created.
    /// </param>
    /// <param name="reporter">
    /// Progress reporter used to emit transfer progress.
    /// </param>
    /// <param name="ct">
    /// Cancellation token used to stop the write operation.
    /// </param>
    /// <returns>
    /// A task that returns the operation result, including any error details if writing fails.
    /// </returns>
    public Task<IResult> WriteDirStreamAsync(
        DirTransferStream dirStream,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    );
}
