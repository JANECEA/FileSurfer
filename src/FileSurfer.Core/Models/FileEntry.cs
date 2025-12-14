using System.IO;

namespace FileSurfer.Core.Models;

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

    public string NameWOExtension =>
        _nameWOExtension ??= Path.GetFileNameWithoutExtension(PathToEntry);
    private string? _nameWOExtension;

    public FileEntry(string pathToFile) => PathToEntry = pathToFile;
}
