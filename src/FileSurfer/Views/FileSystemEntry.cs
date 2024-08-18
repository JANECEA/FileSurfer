using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace FileSurfer;

public class FileSystemEntry
{
    private readonly static Bitmap _folderIcon =
        new(Avalonia.Platform.AssetLoader.Open(new Uri("avares://FileSurfer/Assets/FolderIcon.png")));
    public readonly string PathToEntry;
    public readonly bool IsDirectory;
    public Bitmap? Icon { get; }
    public string Name { get; }
    public DateTime LastChanged { get; }
    public string LastModified { get; }
    public string Size { get; }
    public long? SizeKib { get; }
    public string Type { get; }
    public double Opacity { get; }

    public FileSystemEntry(string path, bool isDirectory, IFileOperationsHandler fileOpsHandler)
    {
        PathToEntry = path;
        IsDirectory = isDirectory;
        Name = Path.GetFileName(path);
        Icon = isDirectory ? _folderIcon : GetIcon(fileOpsHandler);
        LastChanged = fileOpsHandler.GetFileLastModified(path) ?? DateTime.MaxValue;
        LastModified = SetLastModified(fileOpsHandler);
        Opacity = fileOpsHandler.IsHidden(path, isDirectory) || Name.StartsWith('.') ? 0.4 : 1;

        SizeKib = isDirectory 
            ? null
            : (fileOpsHandler.GetFileSizeB(path) + 1023) / 1024;

        Size = isDirectory
            ? string.Empty
            : SizeKib.ToString() + " KiB";

        if (isDirectory)
            Type = "Directory";
        else
        {
            string extension = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            Type = extension == string.Empty ? "File" : extension + " File";
        }
    }

    public FileSystemEntry(string driveName, string volumeLabel)
    {
        PathToEntry = driveName;
        Name = $"{volumeLabel} ({driveName.TrimEnd('\\').TrimEnd('/')})";
        IsDirectory = true;
        Type = "Drive";
        Icon = _folderIcon;
        LastModified = string.Empty;
        Size = string.Empty;
        Opacity = 1;
    }

    private Bitmap? GetIcon(IFileOperationsHandler fileOpsHandler)
    {
        if (fileOpsHandler.GetFileIcon(PathToEntry) is not System.Drawing.Bitmap bitmap)
            return null;

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
