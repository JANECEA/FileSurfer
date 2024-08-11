using System.Drawing;
using System.Drawing.Imaging;
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
        if (fileOpsHandler.GetFileIcon(PathToEntry) is not Icon icon)
            return null;

        System.Drawing.Bitmap bitmap = icon.ToBitmap();
        using MemoryStream stream = new();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        return new Bitmap(stream);
    }
}
