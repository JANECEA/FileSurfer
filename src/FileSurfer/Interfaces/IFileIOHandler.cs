using System;
using System.IO;

namespace FileSurfer.Models;

/// <summary>
/// Defines methods for handling file and directory operations.
/// <para>
/// Includes functionalities for file and directory management such as creating, moving, copying, deleting,
/// and retrieving information about files and directories.
/// </para>
/// It also provides methods for interacting with special folders,
/// system drives, and command-line operations.
/// </summary>
public interface IFileIOHandler
{
    /// <summary>
    /// Gets an array of drives on the system.
    /// </summary>
    public DriveInfo[] GetDrives();

    /// <summary>
    /// Retrieves special folder paths.
    /// </summary>
    public string[] GetSpecialFolders();

    /// <summary>
    /// Opens a file at the specified path.
    /// </summary>
    /// <returns><see langword="true"/> if the file was successfully opened, otherwise <see langword="false"/>.</returns>
    public bool OpenFile(string filePath, out string? errorMessage);

    /// <summary>
    /// Opens a file in Notepad.
    /// </summary>
    /// <returns><see langword="true"/> if the file was successfully opened, otherwise <see langword="false"/>.</returns>
    public bool OpenInNotepad(string filePath, out string? errorMessage);

    /// <summary>
    /// Creates a symbolic link to the specified file path.
    /// </summary>
    /// <returns><see langword="true"/> if the link was successfully created, otherwise <see langword="false"/>.</returns>
    public bool CreateLink(string filePath, out string? errorMessage);

    /// <summary>
    /// Checks if a symbolic link is referring to a directory.
    /// </summary>
    /// <returns><see langword="true"/> if the path is linked to a directory, otherwise <see langword="false"/>.</returns>
    public bool IsLinkedToDirectory(string linkPath, out string? directory);

    /// <summary>
    /// Gets directories in a path, with optional inclusion of hidden and system directories.
    /// </summary>
    /// <returns>An array of directory paths.</returns>
    public string[] GetPathDirs(string path, bool includeHidden, bool includeOS);

    /// <summary>
    /// Gets files in a path, with optional inclusion of hidden and system files.
    /// </summary>
    /// <returns>An array of file paths.</returns>
    public string[] GetPathFiles(string path, bool includeHidden, bool includeOS);

    /// <summary>
    /// Gets the size of a file in bytes.
    /// </summary>
    /// <returns>The size of the file in bytes.</returns>
    public long GetFileSizeB(string path);

    /// <summary>
    /// Gets the last modified date of a file.
    /// </summary>
    /// <returns>The last modified date of the file, or <see langword="null"/> if the file does not exist.</returns>
    public DateTime? GetFileLastModified(string filePath);

    /// <summary>
    /// Gets the last modified date of a directory.
    /// </summary>
    /// <returns>The last modified date of the directory, or <see langword="null"/> if the directory does not exist.</returns>
    public DateTime? GetDirLastModified(string dirPath);

    /// <summary>
    /// Gets the icon associated with a file.
    /// </summary>
    /// <returns>The file's icon, or <see langword="null"/> if the icon cannot be retrieved.</returns>
    public System.Drawing.Bitmap? GetFileIcon(string path);

    /// <summary>
    /// Opens a command prompt at the specified directory path.
    /// </summary>
    /// <returns><see langword="true"/> if the command prompt was successfully opened, otherwise <see langword="false"/>.</returns>
    public bool OpenCmdAt(string dirPath, out string? errorMessage);

    /// <summary>
    /// Executes a command in the command prompt.
    /// </summary>
    /// <returns><see langword="true"/> if the command was successfully executed, otherwise <see langword="false"/>.</returns>
    public bool ExecuteCmd(string command);

    /// <summary>
    /// Checks if a path is hidden.
    /// </summary>
    /// <returns><see langword="true"/> if the path is hidden, otherwise <see langword="false"/>.</returns>
    public bool IsHidden(string path, bool isDirectory);

    /// <summary>
    /// Creates a new file at the specified directory path.
    /// </summary>
    /// <returns><see langword="true"/> if the file was successfully created, otherwise <see langword="false"/>.</returns>
    public bool NewFileAt(string dirPath, string fileName, out string? errorMessage);

    /// <summary>
    /// Creates a new directory at the specified path.
    /// </summary>
    /// <returns><see langword="true"/> if the directory was successfully created, otherwise <see langword="false"/>.</returns>
    public bool NewDirAt(string dirPath, string DirName, out string? errorMessage);

    /// <summary>
    /// Renames a file at the specified path.
    /// </summary>
    /// <returns><see langword="true"/> if the file was successfully renamed, otherwise <see langword="false"/>.</returns>
    public bool RenameFileAt(string filePath, string newName, out string? errorMessage);

    /// <summary>
    /// Renames a directory at the specified path.
    /// </summary>
    /// <returns><see langword="true"/> if the directory was successfully renamed, otherwise <see langword="false"/>.</returns>
    public bool RenameDirAt(string dirPath, string newName, out string? errorMessage);

    /// <summary>
    /// Moves a file to a specified destination directory.
    /// </summary>
    /// <returns><see langword="true"/> if the file was successfully moved, otherwise <see langword="false"/>.</returns>
    public bool MoveFileTo(string filePath, string destinationDir, out string? errorMessage);

    /// <summary>
    /// Moves a directory to a specified destination directory.
    /// </summary>
    /// <returns><see langword="true"/> if the directory was successfully moved, otherwise <see langword="false"/>.</returns>
    public bool MoveDirTo(string dirPath, string destinationDir, out string? errorMessage);

    /// <summary>
    /// Copies a file to a specified destination directory.
    /// </summary>
    /// <returns><see langword="true"/> if the file was successfully copied, otherwise <see langword="false"/>.</returns>
    public bool CopyFileTo(string filePath, string destinationDir, out string? errorMessage);

    /// <summary>
    /// Copies a directory to a specified destination directory.
    /// </summary>
    /// <returns><see langword="true"/> if the directory was successfully copied, otherwise <see langword="false"/>.</returns>
    public bool CopyDirTo(string dirPath, string destinationDir, out string? errorMessage);

    /// <summary>
    /// Creates a duplicate of a file with a new name.
    /// </summary>
    /// <returns><see langword="true"/> if the file was successfully duplicated, otherwise <see langword="false"/>.</returns>
    public bool DuplicateFile(string filePath, string copyName, out string? errorMessage);

    /// <summary>
    /// Creates a duplicate of a directory with a new name.
    /// </summary>
    /// <returns><see langword="true"/> if the directory was successfully duplicated, otherwise <see langword="false"/>.</returns>
    public bool DuplicateDir(string dirPath, string copyName, out string? errorMessage);

    /// <summary>
    /// Moves a file to the trash.
    /// </summary>
    /// <returns><see langword="true"/> if the file was successfully moved to the trash, otherwise <see langword="false"/>.</returns>
    public bool MoveFileToTrash(string filePath, out string? errorMessage);

    /// <summary>
    /// Moves a directory to the trash.
    /// </summary>
    /// <returns><see langword="true"/> if the directory was successfully moved to the trash, otherwise <see langword="false"/>.</returns>
    public bool MoveDirToTrash(string dirPath, out string? errorMessage);

    /// <summary>
    /// Restores a file from the trash to its original location.
    /// </summary>
    /// <returns><see langword="true"/> if the file was successfully restored, otherwise <see langword="false"/>.</returns>
    public bool RestoreFile(string ogFilePath, out string? errorMessage);

    /// <summary>
    /// Restores a directory from the trash to its original location.
    /// </summary>
    /// <returns><see langword="true"/> if the directory was successfully restored, otherwise <see langword="false"/>.</returns>
    public bool RestoreDir(string ogDirPath, out string? errorMessage);

    /// <summary>
    /// Deletes a file permanently.
    /// </summary>
    /// <returns><see langword="true"/> if the file was successfully deleted, otherwise <see langword="false"/>.</returns>
    public bool DeleteFile(string filePath, out string? errorMessage);

    /// <summary>
    /// Deletes a directory permanently.
    /// </summary>
    /// <returns><see langword="true"/> if the directory was successfully deleted, otherwise <see langword="false"/>.</returns>
    public bool DeleteDir(string dirPath, out string? errorMessage);
}
