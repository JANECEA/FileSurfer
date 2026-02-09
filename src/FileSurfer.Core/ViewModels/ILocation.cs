using System;
using FileSurfer.Core.Models;

namespace FileSurfer.Core.ViewModels;

public interface ILocation : IEquatable<ILocation>
{
    public string Path { get; }
    public IFileSystem FileSystem { get; }

    public bool Exists();

    public string Normalize();
}

public sealed class LocalDirLocation : ILocation
{
    public string Path { get; }
    public IFileSystem FileSystem { get; }

    public LocalDirLocation(IFileSystem localFileSystem, string dirPath)
    {
        Path = dirPath;
        FileSystem = localFileSystem;
    }

    public bool Exists() => FileSystem.FileInfoProvider.DirectoryExists(Path);

    public string Normalize() => PathTools.NormalizePath(Path);

    public bool Equals(ILocation? other) =>
        other is LocalDirLocation && PathTools.PathsAreEqual(Path, other.Path);
}

public sealed class SftpDirectoryLocation : ILocation
{
    public string Path { get; }
    public IFileSystem FileSystem { get; }

    public SftpDirectoryLocation(IFileSystem sftpFileSystem, string dirPath)
    {
        Path = dirPath;
        FileSystem = sftpFileSystem;
    }

    public bool Exists() => FileSystem.FileInfoProvider.DirectoryExists(Path);

    public string Normalize() => Path;

    public bool Equals(ILocation? other)
    {
        return false; //TODO
    }
}
