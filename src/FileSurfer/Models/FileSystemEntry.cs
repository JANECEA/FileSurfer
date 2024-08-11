using System.Drawing;
using System.IO;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace FileSurfer;

public class FileSystemEntry
{
    public readonly string PathToEntry;
    public readonly bool IsDirectory;
    private readonly string _name;
    public string Name => _name;
    public Bitmap? Icon { get; }

    public FileSystemEntry(string path, bool isDirectory, IFileOperationsHandler fileOpsHandler)
    {
        PathToEntry = path;
        IsDirectory = isDirectory;
        _name = Path.GetFileName(path);
        Icon = GetIcon(fileOpsHandler);
    }

    private Bitmap? GetIcon(IFileOperationsHandler fileOpsHandler)
    {
        if (fileOpsHandler.GetFileIcon(PathToEntry) is Icon icon)
        {
            using MemoryStream stream = new();
            icon.Save(stream);
            stream.Position = 0;
            return new Bitmap(stream);
        }
        return null;
    }
}
