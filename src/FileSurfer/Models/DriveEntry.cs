using System.IO;

namespace FileSurfer.Models;

/// <summary>
/// Implementation of <see cref="IFileSystemEntry"/> for a drive.
/// </summary>
public sealed class DriveEntry : IFileSystemEntry
{
    public string PathToEntry { get; }

    public string Name { get; }

    public string Extension => string.Empty;

    public string NameWOExtension => Name;

    public DriveEntry(DriveInfo driveInfo)
    {
        PathToEntry = driveInfo.Name;
        Name = !string.IsNullOrEmpty(driveInfo.VolumeLabel)
            ? $"{driveInfo.VolumeLabel} ({driveInfo.Name.TrimEnd(Path.DirectorySeparatorChar)})"
            : driveInfo.Name.TrimEnd(Path.DirectorySeparatorChar);
    }
}
