using FileSurfer.Core.Models;

namespace FileSurfer.Core.Services.FileOperations;

/// <summary>
/// Defines methods for handling file and directory operations.
/// <para/>
/// Includes methods for file and directory management such as creating,
/// moving, copying and deleting files and directories.
/// </summary>
public interface IFileIoHandler
{
    /// <summary>
    /// Creates a new file at the specified directory path.
    /// </summary>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult NewFileAt(string dirPath, string fileName);

    /// <summary>
    /// Creates a new directory at the specified path.
    /// </summary>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult NewDirAt(string dirPath, string dirName);

    /// <summary>
    /// Renames a file at the specified path.
    /// </summary>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult RenameFileAt(string filePath, string newName);

    /// <summary>
    /// Renames a directory at the specified path.
    /// </summary>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult RenameDirAt(string dirPath, string newName);

    /// <summary>
    /// Moves a file to a specified destination directory.
    /// </summary>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult MoveFileTo(string filePath, string destinationDir);

    /// <summary>
    /// Moves a directory to a specified destination directory.
    /// </summary>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult MoveDirTo(string dirPath, string destinationDir);

    /// <summary>
    /// Copies a file to a specified destination directory.
    /// </summary>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult CopyFileTo(string filePath, string destinationDir);

    /// <summary>
    /// Copies a directory to a specified destination directory.
    /// </summary>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult CopyDirTo(string dirPath, string destinationDir);

    /// <summary>
    /// Creates a duplicate of a file with a new name.
    /// </summary>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult DuplicateFile(string filePath, string copyName);

    /// <summary>
    /// Creates a duplicate of a directory with a new name.
    /// </summary>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult DuplicateDir(string dirPath, string copyName);

    /// <summary>
    /// Deletes a file permanently.
    /// </summary>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult DeleteFile(string filePath);

    /// <summary>
    /// Deletes a directory permanently.
    /// </summary>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult DeleteDir(string dirPath);
}

/// <summary>
/// Extends <see cref="IFileIoHandler"/> for remote filesystems
/// </summary>
public interface IRemoteFileIoHandler : IFileIoHandler
{
    /// <summary>
    /// Uploads file from localPath to remotePath
    /// </summary>
    /// <param name="localPath">local path to the file</param>
    /// <param name="remotePath">resulting remote path</param>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult UploadFile(string localPath, string remotePath);

    /// <summary>
    /// Downloads file from remotePath to localPath
    /// </summary>
    /// <param name="remotePath">resulting local path</param>
    /// <param name="localPath">remote path to the file</param>
    /// <returns>An <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult DownloadFile(string remotePath, string localPath);
}
