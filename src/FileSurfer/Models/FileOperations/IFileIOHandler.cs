namespace FileSurfer.Models.FileOperations;

/// <summary>
/// Defines methods for handling file and directory operations.
/// <para>
/// Includes methods for file and directory management such as creating,
/// moving, copying and deleting files and directories.
/// </summary>
public interface IFileIOHandler
{
    /// <summary>
    /// Creates a new file at the specified directory path.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult NewFileAt(string dirPath, string fileName);

    /// <summary>
    /// Creates a new directory at the specified path.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult NewDirAt(string dirPath, string dirName);

    /// <summary>
    /// Renames a file at the specified path.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult RenameFileAt(string filePath, string newName);

    /// <summary>
    /// Renames a directory at the specified path.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult RenameDirAt(string dirPath, string newName);

    /// <summary>
    /// Moves a file to a specified destination directory.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult MoveFileTo(string filePath, string destinationDir);

    /// <summary>
    /// Moves a directory to a specified destination directory.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult MoveDirTo(string dirPath, string destinationDir);

    /// <summary>
    /// Copies a file to a specified destination directory.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult CopyFileTo(string filePath, string destinationDir);

    /// <summary>
    /// Copies a directory to a specified destination directory.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult CopyDirTo(string dirPath, string destinationDir);

    /// <summary>
    /// Creates a duplicate of a file with a new name.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult DuplicateFile(string filePath, string copyName);

    /// <summary>
    /// Creates a duplicate of a directory with a new name.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult DuplicateDir(string dirPath, string copyName);

    /// <summary>
    /// Moves a file to the trash.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult MoveFileToTrash(string filePath);

    /// <summary>
    /// Moves a directory to the trash.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult MoveDirToTrash(string dirPath);

    /// <summary>
    /// Restores a file from the trash to its original location.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult RestoreFile(string ogFilePath);

    /// <summary>
    /// Restores a directory from the trash to its original location.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult RestoreDir(string ogDirPath);

    /// <summary>
    /// Deletes a file permanently.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult DeleteFile(string filePath);

    /// <summary>
    /// Deletes a directory permanently.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult DeleteDir(string dirPath);
}
