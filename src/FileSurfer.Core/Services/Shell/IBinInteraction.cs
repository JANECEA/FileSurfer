using System;
using FileSurfer.Core.Models;

namespace FileSurfer.Core.Services.Shell;

/// <summary>
/// Provides operations for moving entries to trash and restoring them by original path.
/// </summary>
public interface IBinInteraction : IDisposable
{
    /// <summary>
    /// Moves a file to the trash.
    /// </summary>
    /// <param name="filePath">
    /// The full path of the file to move to trash.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if moving the file fails.
    /// </returns>
    public IResult MoveFileToTrash(string filePath);

    /// <summary>
    /// Moves a directory to the trash.
    /// </summary>
    /// <param name="dirPath">
    /// The full path of the directory to move to trash.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if moving the directory fails.
    /// </returns>
    public IResult MoveDirToTrash(string dirPath);

    /// <summary>
    /// Restores a file based on <paramref name="originalFilePath"/>.
    /// </summary>
    /// <param name="originalFilePath">
    /// The original full path of the file before it was moved to trash.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if file restoration fails.
    /// </returns>
    public IResult RestoreFile(string originalFilePath);

    /// <summary>
    /// Restores a directory based on <paramref name="originalDirPath"/>.
    /// </summary>
    /// <param name="originalDirPath">
    /// The original full path of the directory before it was moved to trash.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if directory restoration fails.
    /// </returns>
    public IResult RestoreDir(string originalDirPath);
}
