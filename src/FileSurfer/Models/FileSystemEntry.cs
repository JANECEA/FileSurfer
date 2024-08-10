using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Avalonia.Media.Imaging;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace FileSurfer;

public class FileSystemEntry
{
    public readonly string PathToEntry;
    public readonly bool IsDirectory;
    private readonly string _name;
    public string Name => _name;
    public Bitmap? Icon => GetIcon();

    public FileSystemEntry(string path, bool isDirectory)
    {
        PathToEntry = path;
        IsDirectory = isDirectory;
        _name = Path.GetFileName(path);
    }

    private Bitmap? GetIcon()
    {
        return null;
    }
}
