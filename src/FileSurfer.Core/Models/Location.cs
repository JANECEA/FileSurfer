using System.Threading.Tasks;

namespace FileSurfer.Core.Models;

/// <summary>
/// Represents a directory location path bound to a specific file system.
/// </summary>
public sealed class Location
{
    /// <summary>
    /// Gets the full path to the location.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the file system that owns this location.
    /// </summary>
    public IFileSystem FileSystem { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Location"/> class.
    /// </summary>
    /// <param name="fileSystem">
    /// File system that owns the provided path.
    /// </param>
    /// <param name="dirPath">
    /// Directory path represented by this location.
    /// </param>
    public Location(IFileSystem fileSystem, string dirPath)
    {
        Path = dirPath;
        FileSystem = fileSystem;
    }

    /// <summary>
    /// Determines whether this directory exists.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the file system is ready and the directory exists; otherwise <see langword="false"/>.
    /// </returns>
    public bool Exists() => FileSystem.IsReady() && FileSystem.FileInfoProvider.Exists(Path).AsDir;

    /// <summary>
    /// Determines asynchronously whether this directory exists.
    /// </summary>
    /// <returns>
    /// A task that returns <see langword="true"/> when the file system is ready and the directory exists; otherwise <see langword="false"/>.
    /// </returns>
    public async Task<bool> ExistsAsync()
    {
        if (!FileSystem.IsReady())
            return false;

        return (await FileSystem.FileInfoProvider.ExistsAsync(Path)).AsDir;
    }

    /// <summary>
    /// Determines whether this location points to the same directory as another location.
    /// </summary>
    /// <param name="other">
    /// Location to compare with.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when both locations represent the same directory in the same file-system context; otherwise <see langword="false"/>.
    /// </returns>
    public bool IsSame(Location? other)
    {
        if (other is null)
            return false;

        if (FileSystem.IsLocal() && other.FileSystem.IsLocal())
            return LocalPathTools.PathsAreEqual(Path, other.Path);

        return FileSystem == other.FileSystem
            && FileSystem.FileInfoProvider.PathTools.PathsAreEqual(Path, other.Path);
    }

    public override string ToString() => $"{FileSystem.GetLabel()}:{Path}";
}
