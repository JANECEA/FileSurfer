using System.IO;

namespace FileSurfer.Models;

public class FileEntry : IFileSystemEntry
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

    public FileEntry(string pathToFile, string fileName)
    {
        _name = fileName;
        PathToEntry = pathToFile;
    }
}
