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
/// Lazy implementation of <see cref="IFileSystemEntry"/> for a file.
/// </summary>
public sealed class FileEntry : IFileSystemEntry
{
    public string PathToEntry { get; }

    public string Name => _name ??= Path.GetFileName(PathToEntry);
    private string? _name;

    public string Extension => _extension ??= Path.GetExtension(PathToEntry);
    private string? _extension;

    public string NameWoExtension =>
        _nameWoExtension ??= Path.GetFileNameWithoutExtension(PathToEntry);
    private string? _nameWoExtension;

    public FileEntry(string pathToFile) => PathToEntry = pathToFile;
}

/// <summary>
/// Lazy implementation of <see cref="IFileSystemEntry"/> for a directory.
/// </summary>
public sealed class DirectoryEntry : IFileSystemEntry
{
    public string PathToEntry { get; }

    public string Name => _name ??= Path.GetFileName(PathToEntry);
    private string? _name;

    string IFileSystemEntry.Extension => string.Empty;
    string IFileSystemEntry.NameWoExtension => Name;

    public DirectoryEntry(string dirPath) => PathToEntry = dirPath;
}

/// <summary>
/// Implementation of <see cref="IFileSystemEntry"/> for a drive.
/// </summary>
public sealed class DriveEntry : IFileSystemEntry
{
    public string PathToEntry { get; }
    public string Name { get; }
    public long SizeB { get; }
    string IFileSystemEntry.Extension => string.Empty;
    string IFileSystemEntry.NameWoExtension => Name;

    public DriveEntry(DriveInfo driveInfo)
    {
        PathToEntry = driveInfo.Name;
        Name = !string.IsNullOrEmpty(driveInfo.VolumeLabel)
            ? $"{driveInfo.VolumeLabel} ({driveInfo.Name.TrimEnd(Path.DirectorySeparatorChar)})"
            : driveInfo.Name.TrimEnd(Path.DirectorySeparatorChar);

        SizeB = driveInfo.TotalFreeSpace;
    }

    public DriveEntry(string pathToEntry, string name, long sizeB)
    {
        PathToEntry = pathToEntry;
        Name = name;
        SizeB = sizeB;
    }
}
