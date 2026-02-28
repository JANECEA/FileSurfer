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
/// Implementation of <see cref="IFileSystemEntry"/> for a drive.
/// </summary>
public sealed class DriveEntry : IFileSystemEntry
{
    public string PathToEntry { get; }
    public string Name { get; }
    string IFileSystemEntry.Extension => string.Empty;
    string IFileSystemEntry.NameWoExtension => Name;

    public DriveEntry(DriveInfo driveInfo)
    {
        PathToEntry = driveInfo.Name;
        Name = !string.IsNullOrEmpty(driveInfo.VolumeLabel)
            ? $"{driveInfo.VolumeLabel} ({driveInfo.Name.TrimEnd(Path.DirectorySeparatorChar)})"
            : driveInfo.Name.TrimEnd(Path.DirectorySeparatorChar);
    }

    public DriveEntry(string pathToEntry, string name)
    {
        PathToEntry = pathToEntry;
        Name = name;
    }
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

    public FileEntry(string pathToFile)
    {
        PathToEntry = pathToFile;
        Name = Path.GetFileName(pathToFile);
        Extension = Path.GetExtension(pathToFile);
        NameWoExtension = Path.GetFileNameWithoutExtension(pathToFile);
    }

    private protected FileEntry(string pathToFile, string name)
    {
        PathToEntry = pathToFile;
        Name = name;
        Extension = Path.GetExtension(pathToFile);
        NameWoExtension = Path.GetFileNameWithoutExtension(pathToFile);
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

    public DirectoryEntry(string dirPath)
    {
        PathToEntry = dirPath;
        Name = Path.GetFileName(dirPath);
    }

    private protected DirectoryEntry(string dirPath, string name)
    {
        PathToEntry = dirPath;
        Name = name;
    }
}

/// <summary>
/// Represents a universal file info object
/// </summary>
public class FileEntryInfo : FileEntry
{
    public DateTime LastModified { get; }
    public DateTime LastModifiedUtc { get; }
    public long SizeB { get; }

    public FileEntryInfo(
        string pathToFile,
        string name,
        long sizeB,
        DateTime lastModified,
        DateTime lastModifiedUtc
    )
        : base(pathToFile, name)
    {
        LastModified = lastModified;
        LastModifiedUtc = lastModifiedUtc;
        SizeB = sizeB;
    }

    public FileEntryInfo(FileInfo fileInfo)
        : base(fileInfo.FullName, fileInfo.Name)
    {
        LastModified = fileInfo.LastWriteTime;
        LastModifiedUtc = fileInfo.LastWriteTimeUtc;
        SizeB = fileInfo.Length;
    }
}

/// <summary>
/// Represents a universal Directory info object
/// </summary>
public class DirectoryEntryInfo : DirectoryEntry
{
    public DateTime LastModified { get; }
    public DateTime LastModifiedUtc { get; }

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

    public DirectoryEntryInfo(DirectoryInfo dirInfo)
        : base(dirInfo.FullName, dirInfo.Name)
    {
        LastModified = dirInfo.LastWriteTime;
        LastModifiedUtc = dirInfo.LastWriteTimeUtc;
    }
}
