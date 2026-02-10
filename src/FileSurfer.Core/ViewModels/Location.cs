using FileSurfer.Core.Models;

namespace FileSurfer.Core.ViewModels;

/// <summary>
/// TODO
/// </summary>
public sealed class Location
{
    /// <summary>
    /// TODO
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// TODO
    /// </summary>
    public IFileSystem FileSystem { get; }

    public Location(IFileSystem localFileSystem, string dirPath)
    {
        Path = dirPath;
        FileSystem = localFileSystem;
    }

    public bool Exists() => FileSystem.FileInfoProvider.DirectoryExists(Path);

    public bool Equals(Location? other)
    {
        if (other is null)
            return false;

        if (FileSystem is LocalFileSystem && other.FileSystem is LocalFileSystem)
            return PathTools.PathsAreEqual(Path, other.Path);

        return FileSystem == other.FileSystem && Path == other.Path;
    }
}
