using System.IO;

namespace FileSurfer.Models;

public class DirectoryEntry : IFileSystemEntry
{
    public string PathToEntry { get; }

    public string Name => _name ??= Path.GetFileName(PathToEntry);
    private string? _name;

    public string Extension => string.Empty;

    public string NameWOExtension => Name;

    public DirectoryEntry(string dirPath) => PathToEntry = dirPath;

    public DirectoryEntry(string dirPath, string dirName)
    {
        _name = dirName;
        PathToEntry = dirPath;
    }
}
