using System.IO;

namespace FileSurfer.Core.Models;

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
