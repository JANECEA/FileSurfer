using FileSurfer.Core.Models;

namespace FileSurfer.Core.ViewModels;

/// <summary>
/// Represents a generic location (path) with its file system
/// </summary>
public sealed class Location
{
    /// <summary>
    /// Full path to the location
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The relevant file system
    /// </summary>
    public IFileSystem FileSystem { get; }

    public Location(IFileSystem fileSystem, string dirPath)
    {
        Path = dirPath;
        FileSystem = fileSystem;
    }

    public bool Exists() =>
        FileSystem.IsReady() && FileSystem.FileInfoProvider.DirectoryExists(Path);

    public bool Equals(Location? other)
    {
        if (other is null)
            return false;

        if (FileSystem is LocalFileSystem && other.FileSystem is LocalFileSystem)
            return PathTools.PathsAreEqual(Path, other.Path);

        return FileSystem == other.FileSystem && Path == other.Path;
    }
}
