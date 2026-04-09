using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable UnusedMember.Global

namespace FileSurfer.Core.Models.FileInformation;

public readonly struct ExistsInfo
{
    public bool AsPath => AsFile || AsDir;
    public bool AsFile { get; }
    public bool AsDir { get; }

    private ExistsInfo(bool asFile, bool asDir)
    {
        AsFile = asFile;
        AsDir = asDir;
    }

    public static ExistsInfo ExistsAsFile() => new(true, false);

    public static ExistsInfo ExistsAsDirectory() => new(false, true);

    public static ExistsInfo DoesNotExist() => new(false, false);
}

public sealed class DirectoryContents
{
    public required IReadOnlyList<DirectoryEntryInfo> Dirs { get; init; }
    public required IReadOnlyList<FileEntryInfo> Files { get; init; }
}

/// <summary>
/// Defines methods for retrieving file and directory information.
/// </summary>
public interface IFileInfoProvider
{
    /// <summary>
    /// Provides methods for manipulating paths for specific filesystems
    /// </summary>
    public IPathTools PathTools { get; }

    /// <summary>
    /// Checks if a symbolic link is referring to a directory.
    /// </summary>
    /// <returns><see langword="true"/> if the path is linked to a directory, otherwise <see langword="false"/>.</returns>
    public bool IsLinkedToDirectory(string linkPath, [NotNullWhen(true)] out string? directory);

    /// <summary>
    /// Gets files and directories in a directory, with optional inclusion of hidden and system entries.
    /// </summary>
    public ValueResult<DirectoryContents> GetPathEntries(
        string path,
        bool includeHidden,
        bool includeOs
    );

    /// <summary>
    /// Asynchronously gets files in a directory, with optional inclusion of hidden and system entries.
    /// </summary>
    public Task<ValueResult<DirectoryContents>> GetPathEntriesAsync(
        string path,
        bool includeHidden,
        bool includeOs,
        CancellationToken ct
    );

    /// <summary>
    /// Returns an opened file stream for reading on the specified path.
    /// </summary>
    /// <param name="path">Path to the file</param>
    public ValueResult<Stream> GetFileStream(string path);

    /// <summary>
    /// Gets the last modified Utc time of a file.
    /// </summary>
    /// <returns>The last modified date of the file, or <see langword="null"/> if the file does not exist.</returns>
    public Task<DateTime?> GetFileLastWriteUtcAsync(string filePath);

    /// <summary>
    /// Gets the last modified Utc time of a directory.
    /// </summary>
    /// <returns>The last modified date of the directory, or <see langword="null"/> if the directory does not exist.</returns>
    public Task<DateTime?> GetDirLastWriteUtcAsync(string dirPath);

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
    /// Determines if the file or directory exists within the containing file system.
    /// </summary>
    public ExistsInfo Exists(string path);

    /// <summary>
    /// Asynchronously determines if the file or directory exists within the containing file system.
    /// </summary>
    public Task<ExistsInfo> ExistsAsync(string path);
}

/// <summary>
/// Defines methods for retrieving information specific to local file systems
/// </summary>
public interface ILocalFileInfoProvider : IFileInfoProvider
{
    /// <summary>
    /// Gets an array of drives on the system.
    /// </summary>
    public DriveEntryInfo[] GetDrives();

    /// <summary>
    /// Retrieves special folder paths.
    /// </summary>
    public IEnumerable<string> GetSpecialFolders();
}
