using System;
using System.IO;

namespace FileSurfer.Core.Models;

/// <summary>
/// Represents a file system entry in context of the <see cref="FileSurfer"/> app.
/// </summary>
public interface IFileSystemEntry
{
    /// <summary>
    /// Path to the file, directory, or drive represented by this <see cref="IFileSystemEntry"/>.
    /// </summary>
    public string PathToEntry { get; }

    /// <summary>
    /// Holds the name of file, directory, or drive represented by this <see cref="IFileSystemEntry"/>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Holds the extension of this file's name
    /// </summary>
    public string Extension { get; }

    /// <summary>
    /// Holds this entry's name without the extension
    /// </summary>
    public string NameWoExtension { get; }
}

/// <summary>
/// Implementation of <see cref="IFileSystemEntry"/> for a file.
/// </summary>
public class FileEntry : IFileSystemEntry
{
    public string PathToEntry { get; }

    public string Name { get; }

    public string Extension { get; }

    public string NameWoExtension { get; }

    /// <summary>
    /// Initializes a new <see cref="FileEntry"/> from a path using path-tool name and extension parsing.
    /// </summary>
    /// <param name="pathToFile">
    /// Full path to the file.
    /// </param>
    /// <param name="pathTools">
    /// Path utilities used to derive file name and extension.
    /// </param>
    public FileEntry(string pathToFile, IPathTools pathTools)
    {
        PathToEntry = pathToFile;
        Name = pathTools.GetFileName(pathToFile);
        Extension = pathTools.GetExtension(pathToFile);

        NameWoExtension = string.IsNullOrEmpty(Extension) ? Name : Name[..^Extension.Length];
    }

    /// <summary>
    /// Initializes a new <see cref="FileEntry"/> from precomputed path, name, and extension values.
    /// </summary>
    /// <param name="pathToFile">
    /// Full path to the file.
    /// </param>
    /// <param name="name">
    /// File name.
    /// </param>
    /// <param name="extension">
    /// File extension including dot, or an empty string.
    /// </param>
    private protected FileEntry(string pathToFile, string name, string extension)
    {
        PathToEntry = pathToFile;
        Name = name;
        Extension = extension;

        NameWoExtension = string.IsNullOrEmpty(Extension) ? name : name[..^Extension.Length];
    }
}

/// <summary>
/// Implementation of <see cref="IFileSystemEntry"/> for a directory.
/// </summary>
public class DirectoryEntry : IFileSystemEntry
{
    public string PathToEntry { get; }

    public string Name { get; }

    string IFileSystemEntry.Extension => string.Empty;
    string IFileSystemEntry.NameWoExtension => Name;

    /// <summary>
    /// Initializes a new <see cref="DirectoryEntry"/> from a path using path-tool name parsing.
    /// </summary>
    /// <param name="dirPath">
    /// Full path to the directory.
    /// </param>
    /// <param name="pathTools">
    /// Path utilities used to derive directory name.
    /// </param>
    public DirectoryEntry(string dirPath, IPathTools pathTools)
    {
        PathToEntry = dirPath;
        Name = pathTools.GetFileName(dirPath);
    }

    /// <summary>
    /// Initializes a new <see cref="DirectoryEntry"/> from precomputed path and name values.
    /// </summary>
    /// <param name="dirPath">
    /// Full path to the directory.
    /// </param>
    /// <param name="name">
    /// Directory name.
    /// </param>
    private protected DirectoryEntry(string dirPath, string name)
    {
        PathToEntry = dirPath;
        Name = name;
    }
}

/// <summary>
/// Represents file metadata and identity information.
/// </summary>
public class FileEntryInfo : FileEntry
{
    /// <summary>
    /// Gets the file last modification time in local time.
    /// </summary>
    public DateTime LastModified { get; }

    /// <summary>
    /// Gets the file last modification time in UTC.
    /// </summary>
    public DateTime LastModifiedUtc { get; }

    /// <summary>
    /// Gets file size in bytes.
    /// </summary>
    public long SizeB { get; }

    /// <summary>
    /// Initializes a new <see cref="FileEntryInfo"/> from explicit metadata values.
    /// </summary>
    /// <param name="pathToFile">
    /// Full path to the file.
    /// </param>
    /// <param name="name">
    /// File name.
    /// </param>
    /// <param name="extension">
    /// File extension including dot, or an empty string.
    /// </param>
    /// <param name="sizeB">
    /// File size in bytes.
    /// </param>
    /// <param name="lastModified">
    /// Last modification time in local time.
    /// </param>
    /// <param name="lastModifiedUtc">
    /// Last modification time in UTC.
    /// </param>
    public FileEntryInfo(
        string pathToFile,
        string name,
        string extension,
        long sizeB,
        DateTime lastModified,
        DateTime lastModifiedUtc
    )
        : base(pathToFile, name, extension)
    {
        LastModified = lastModified;
        LastModifiedUtc = lastModifiedUtc;
        SizeB = sizeB;
    }

    /// <summary>
    /// Initializes a new <see cref="FileEntryInfo"/> from a <see cref="FileInfo"/> instance.
    /// </summary>
    /// <param name="fileInfo">
    /// Source file information.
    /// </param>
    public FileEntryInfo(FileInfo fileInfo)
        : base(fileInfo.FullName, fileInfo.Name, fileInfo.Extension)
    {
        LastModified = fileInfo.LastWriteTime;
        LastModifiedUtc = fileInfo.LastWriteTimeUtc;
        SizeB = fileInfo.Length;
    }
}

/// <summary>
/// Represents directory metadata and identity information.
/// </summary>
public class DirectoryEntryInfo : DirectoryEntry
{
    /// <summary>
    /// Gets the directory last modification time in local time.
    /// </summary>
    public DateTime LastModified { get; }

    /// <summary>
    /// Gets the directory last modification time in UTC.
    /// </summary>
    public DateTime LastModifiedUtc { get; }

    /// <summary>
    /// Initializes a new <see cref="DirectoryEntryInfo"/> from explicit metadata values.
    /// </summary>
    /// <param name="dirPath">
    /// Full path to the directory.
    /// </param>
    /// <param name="name">
    /// Directory name.
    /// </param>
    /// <param name="lastModified">
    /// Last modification time in local time.
    /// </param>
    /// <param name="lastModifiedUtc">
    /// Last modification time in UTC.
    /// </param>
    public DirectoryEntryInfo(
        string dirPath,
        string name,
        DateTime lastModified,
        DateTime lastModifiedUtc
    )
        : base(dirPath, name)
    {
        LastModified = lastModified;
        LastModifiedUtc = lastModifiedUtc;
    }

    /// <summary>
    /// Initializes a new <see cref="DirectoryEntryInfo"/> from a <see cref="DirectoryInfo"/> instance.
    /// </summary>
    /// <param name="dirInfo">
    /// Source directory information.
    /// </param>
    public DirectoryEntryInfo(DirectoryInfo dirInfo)
        : base(dirInfo.FullName, dirInfo.Name)
    {
        LastModified = dirInfo.LastWriteTime;
        LastModifiedUtc = dirInfo.LastWriteTimeUtc;
    }
}

/// <summary>
/// Represents drive identity information.
/// </summary>
public sealed class DriveEntryInfo
{
    /// <summary>
    /// Gets the path identifying the drive.
    /// </summary>
    public string PathToEntry { get; }

    /// <summary>
    /// Gets the display name of the drive.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new <see cref="DriveEntryInfo"/> from explicit path and name values.
    /// </summary>
    /// <param name="pathToEntry">
    /// Drive path.
    /// </param>
    /// <param name="name">
    /// Drive display name.
    /// </param>
    public DriveEntryInfo(string pathToEntry, string name)
    {
        PathToEntry = pathToEntry;
        Name = name;
    }

    /// <summary>
    /// Initializes a new <see cref="DriveEntryInfo"/> from a <see cref="DriveInfo"/> instance.
    /// </summary>
    /// <param name="driveInfo">
    /// Source drive information.
    /// </param>
    public DriveEntryInfo(DriveInfo driveInfo)
    {
        PathToEntry = driveInfo.Name;
        Name = !string.IsNullOrEmpty(driveInfo.VolumeLabel)
            ? $"{driveInfo.VolumeLabel} ({driveInfo.Name.TrimEnd(Path.DirectorySeparatorChar)})"
            : driveInfo.Name;
    }
}
