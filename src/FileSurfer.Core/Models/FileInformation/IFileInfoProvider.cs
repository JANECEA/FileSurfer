using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable UnusedMemberInSuper.Global - Methods included for parity
// ReSharper disable UnusedMember.Global

namespace FileSurfer.Core.Models.FileInformation;

/// <summary>
/// Represents whether a path exists and what kind of entry it resolves to.
/// </summary>
public readonly struct ExistsInfo
{
    /// <summary>
    /// Gets a value indicating whether the path exists as either a file or a directory.
    /// </summary>
    public bool AsPath => AsFile || AsDir;

    /// <summary>
    /// Gets a value indicating whether the path exists as a file.
    /// </summary>
    public bool AsFile { get; }

    /// <summary>
    /// Gets a value indicating whether the path exists as a directory.
    /// </summary>
    public bool AsDir { get; }

    private ExistsInfo(bool asFile, bool asDir)
    {
        AsFile = asFile;
        AsDir = asDir;
    }

    /// <summary>
    /// Creates an <see cref="ExistsInfo"/> that represents an existing file.
    /// </summary>
    public static ExistsInfo ExistsAsFile() => new(true, false);

    /// <summary>
    /// Creates an <see cref="ExistsInfo"/> that represents an existing directory.
    /// </summary>
    public static ExistsInfo ExistsAsDirectory() => new(false, true);

    /// <summary>
    /// Creates an <see cref="ExistsInfo"/> that represents a missing path.
    /// </summary>
    public static ExistsInfo DoesNotExist() => new(false, false);
}

/// <summary>
/// Represents the files and directories contained in a single directory.
/// </summary>
public sealed class DirectoryContents
{
    /// <summary>
    /// Gets the directories discovered in the requested path.
    /// </summary>
    public required IReadOnlyList<DirectoryEntryInfo> Dirs { get; init; }

    /// <summary>
    /// Gets the files discovered in the requested path.
    /// </summary>
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
    /// <param name="linkPath">The symbolic link path to inspect.</param>
    /// <param name="directory">When this method returns <see langword="true"/>, contains the linked directory path.</param>
    /// <returns><see langword="true"/> if the path is linked to a directory, otherwise <see langword="false"/>.</returns>
    public bool IsLinkedToDirectory(string linkPath, [NotNullWhen(true)] out string? directory);

    /// <summary>
    /// Gets files and directories in a directory, with optional inclusion of hidden and system entries.
    /// </summary>
    /// <param name="path">The directory path to enumerate.</param>
    /// <param name="includeHidden"><see langword="true"/> to include hidden entries.</param>
    /// <param name="includeOs"><see langword="true"/> to include OS-protected/system entries.</param>
    /// <returns>The directory entries retrieval result.</returns>
    public ValueResult<DirectoryContents> GetPathEntries(
        string path,
        bool includeHidden,
        bool includeOs
    );

    /// <summary>
    /// Asynchronously gets files in a directory, with optional inclusion of hidden and system entries.
    /// </summary>
    /// <param name="path">The directory path to enumerate.</param>
    /// <param name="includeHidden"><see langword="true"/> to include hidden entries.</param>
    /// <param name="includeOs"><see langword="true"/> to include OS-protected/system entries.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The directory entries retrieval result.</returns>
    public Task<ValueResult<DirectoryContents>> GetPathEntriesAsync(
        string path,
        bool includeHidden,
        bool includeOs,
        CancellationToken ct
    );

    /// <summary>
    /// Returns an opened file stream for reading on the specified path.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <returns>The stream retrieval result.</returns>
    public ValueResult<Stream> GetFileStream(string path);

    /// <summary>
    /// Gets the last modified Utc time of a file.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The last modified date of the file, or <see langword="null"/> if the file does not exist.</returns>
    public DateTime? GetFileLastWriteUtc(string filePath);

    /// <summary>
    /// Asynchronously gets the last modified Utc time of a file.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The last modified date of the file, or <see langword="null"/> if the file does not exist.</returns>
    public Task<DateTime?> GetFileLastWriteUtcAsync(string filePath);

    /// <summary>
    /// Gets the last modified Utc time of a directory.
    /// </summary>
    /// <param name="dirPath">The directory path.</param>
    /// <returns>The last modified date of the directory, or <see langword="null"/> if the directory does not exist.</returns>
    public DateTime? GetDirLastWriteUtc(string dirPath);

    /// <summary>
    /// Asynchronously gets the last modified Utc time of a directory.
    /// </summary>
    /// <param name="dirPath">The directory path.</param>
    /// <returns>The last modified date of the directory, or <see langword="null"/> if the directory does not exist.</returns>
    public Task<DateTime?> GetDirLastWriteUtcAsync(string dirPath);

    /// <summary>
    /// Checks if a path is hidden.
    /// </summary>
    /// <param name="path">The path to evaluate.</param>
    /// <param name="isDirectory"><see langword="true"/> when the path is a directory; otherwise it is treated as a file.</param>
    /// <returns><see langword="true"/> if the path is hidden, otherwise <see langword="false"/>.</returns>
    public bool IsHidden(string path, bool isDirectory);

    /// <summary>
    /// Returns the root of the containing filesystem
    /// </summary>
    /// <returns>The filesystem root path.</returns>
    public string GetRoot();

    /// <summary>
    /// Determines if the file or directory exists within the containing file system.
    /// </summary>
    /// <param name="path">The path to evaluate.</param>
    /// <returns>The existence details for the supplied path.</returns>
    public ExistsInfo Exists(string path);

    /// <summary>
    /// Asynchronously determines if the file or directory exists within the containing file system.
    /// </summary>
    /// <param name="path">The path to evaluate.</param>
    /// <returns>The existence details for the supplied path.</returns>
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
    /// <returns>The available drives.</returns>
    public DriveEntryInfo[] GetDrives();

    /// <summary>
    /// Retrieves special folder paths.
    /// </summary>
    /// <returns>A sequence of special folder paths.</returns>
    public IEnumerable<string> GetSpecialFolders();
}
