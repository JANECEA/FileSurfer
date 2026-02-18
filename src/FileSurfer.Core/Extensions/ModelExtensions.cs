using FileSurfer.Core.Models;

namespace FileSurfer.Core.Extensions;

public static class FileSystemExtensions
{
    public static Location GetLocation(this IFileSystem fileSystem, string path) =>
        new(fileSystem, path);
}
