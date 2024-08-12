using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace FileSurfer;

public class FileSystemEntry
{
    public readonly string PathToEntry;
    public readonly bool IsDirectory;
    public Bitmap? Icon { get; }
    public string Name { get; }
    public DateTime LastModfiedTime { get; }
    public string LastModified { get; }
    public string Size { get; }
    public string Type { get; }

    public FileSystemEntry(string path, bool isDirectory, IFileOperationsHandler fileOpsHandler)
    {
        PathToEntry = path;
        IsDirectory = isDirectory;
        Name = Path.GetFileName(path);
        Icon = GetIcon(fileOpsHandler);
        LastModfiedTime = fileOpsHandler.GetFileLastModified(path) ?? DateTime.MaxValue;
        LastModified = SetLastModified(fileOpsHandler);

        Size = isDirectory ? 
            string.Empty : 
            ((fileOpsHandler.GetFileSizeB(path) + 1023) / 1024).ToString() + " KiB";

        if (!isDirectory)
        {
            string extension = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            Type = extension == string.Empty ? "File" : extension + " File";
        }
        else
            Type = "Directory";
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

    private string SetLastModified(IFileOperationsHandler fileOpsHandler)
    {
        DateTime? time = IsDirectory ? 
            fileOpsHandler.GetDirLastModified(PathToEntry) : 
            fileOpsHandler.GetFileLastModified(PathToEntry);

        if (time is DateTime notNullTime)
            return notNullTime.ToShortDateString() + " " + notNullTime.ToShortTimeString();

        return "Error";
    }
}
