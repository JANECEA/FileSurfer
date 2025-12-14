namespace FileSurfer.Core.Models.FileOperations;

/// <summary>
/// Defines methods for handling file and directory operations.
/// <para/>
/// Includes methods for file and directory management such as creating,
/// moving, copying and deleting files and directories.
/// </summary>
public interface IFileIOHandler
{
    /// <summary>
    /// Creates a new file at the specified directory path.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult NewFileAt(string dirPath, string fileName);

    /// <summary>
    /// Creates a new directory at the specified path.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult NewDirAt(string dirPath, string dirName);

    /// <summary>
    /// Renames a file at the specified path.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult RenameFileAt(string filePath, string newName);

    /// <summary>
    /// Renames a directory at the specified path.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult RenameDirAt(string dirPath, string newName);

    /// <summary>
    /// Moves a file to a specified destination directory.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult MoveFileTo(string filePath, string destinationDir);

    /// <summary>
    /// Moves a directory to a specified destination directory.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult MoveDirTo(string dirPath, string destinationDir);

    /// <summary>
    /// Copies a file to a specified destination directory.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult CopyFileTo(string filePath, string destinationDir);

    /// <summary>
    /// Copies a directory to a specified destination directory.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult CopyDirTo(string dirPath, string destinationDir);

    /// <summary>
    /// Creates a duplicate of a file with a new name.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult DuplicateFile(string filePath, string copyName);

    /// <summary>
    /// Creates a duplicate of a directory with a new name.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult DuplicateDir(string dirPath, string copyName);

    /// <summary>
    /// Deletes a file permanently.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult DeleteFile(string filePath);

    /// <summary>
    /// Deletes a directory permanently.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult DeleteDir(string dirPath);
}
