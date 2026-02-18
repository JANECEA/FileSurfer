using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace FileSurfer.Core.Models.FileInformation;

/// <summary>
/// Defines methods for retrieving file and directory information.
/// </summary>
[
    SuppressMessage("ReSharper", "RedundantVirtualModifier"),
    SuppressMessage("ReSharper", "UnusedMember.Global"),
]
public interface IFileInfoProvider
{
    /// <summary>
    /// Checks if a symbolic link is referring to a directory.
    /// </summary>
    /// <returns><see langword="true"/> if the path is linked to a directory, otherwise <see langword="false"/>.</returns>
    public bool IsLinkedToDirectory(string linkPath, out string? directory);

    /// <summary>
    /// Gets directories in a path, with optional inclusion of hidden and system directories.
    /// </summary>
    public ValueResult<List<DirectoryEntryInfo>> GetPathDirs(
        string path,
        bool includeHidden,
        bool includeOs
    );

    /// <summary>
    /// Gets files in a path, with optional inclusion of hidden and system files.
    /// </summary>
    public ValueResult<List<FileEntryInfo>> GetPathFiles(
        string path,
        bool includeHidden,
        bool includeOs
    );

    /// <summary>
    /// Gets the size of a file in bytes.
    /// </summary>
    /// <returns>The size of the file in bytes.</returns>
    public long GetFileSizeB(string path);

    /// <summary>
    /// Gets the last modified Utc time of a file.
    /// </summary>
    /// <returns>The last modified date of the file, or <see langword="null"/> if the file does not exist.</returns>
    public DateTime? GetFileLastModifiedUtc(string filePath);

    /// <summary>
    /// Gets the last modified Utc time of a directory.
    /// </summary>
    /// <returns>The last modified date of the directory, or <see langword="null"/> if the directory does not exist.</returns>
    public DateTime? GetDirLastModifiedUtc(string dirPath);

    /// <summary>
    /// Checks if a path is hidden.
    /// </summary>
    /// <returns><see langword="true"/> if the path is hidden, otherwise <see langword="false"/>.</returns>
    public bool IsHidden(string path, bool isDirectory);

    /// <summary>
    /// Returns the root of the containing filesystem
    /// </summary>
    public string GetRoot();

    /// <summary>
    /// Determines if the file exists within the containing file system.
    /// </summary>
    public virtual bool FileExists(string path) => File.Exists(path);

    /// <summary>
    /// Determines if the directory exists within the containing file system.
    /// </summary>
    public virtual bool DirectoryExists(string path) => Directory.Exists(path);

    /// <summary>
    /// Determines if the file or directory exists  within the containing file system.
    /// </summary>
    public virtual bool PathExists(string path) => Path.Exists(path);
}

/// <summary>
/// Defines methods for retrieving information specific to local file systems
/// </summary>
public interface ILocalFileInfoProvider : IFileInfoProvider
{
    /// <summary>
    /// Gets an array of drives on the system.
    /// </summary>
    public DriveEntry[] GetDrives();

    /// <summary>
    /// Retrieves special folder paths.
    /// </summary>
    public IEnumerable<string> GetSpecialFolders();
}
