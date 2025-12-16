using System.IO;

namespace FileSurfer.Core.Models;

/// <summary>
/// Implementation of <see cref="IFileSystemEntry"/> for a drive.
/// </summary>
public sealed class DriveEntry : IFileSystemEntry
{
    public string PathToEntry { get; }
    public string Name { get; }
    public long SizeB { get; }
    string IFileSystemEntry.Extension => string.Empty;
    string IFileSystemEntry.NameWOExtension => Name;

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
