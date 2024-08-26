using System;
using System.Drawing.Imaging;
using System.IO;
using FileSurfer.Models;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace FileSurfer;

public class FileSystemEntry
{
    private static readonly int SizeLimit = FileSurferSettings.FileSizeDisplayLimit;
    private static readonly Bitmap _folderIcon =
        new(
            Avalonia.Platform.AssetLoader.Open(new Uri("avares://FileSurfer/Assets/FolderIcon.png"))
        );
    private static readonly Bitmap _driveIcon =
        new(
            Avalonia.Platform.AssetLoader.Open(new Uri("avares://FileSurfer/Assets/DriveIcon.png"))
        );
    private static readonly string[] _byteUnits = new string[]
    {
        "B",
        "KiB",
        "MiB",
        "GiB",
        "TiB",
        "PiB",
    };

    public readonly string PathToEntry;
    public bool IsDirectory { get; }
    public Bitmap? Icon { get; }
    public string Name { get; }
    public DateTime LastModTime { get; }
    public string LastModified { get; }
    public long? SizeB { get; }
    public string Size { get; }
    public string Type { get; }
    public double Opacity { get; } = 1;
    public bool VersionControlled { get; } = false;
    public bool Staged { get; } = false;
    public bool IsArchived { get; } = false;

    public FileSystemEntry(
        IFileOperationsHandler fileOpsHandler,
        string path,
        bool isDirectory,
        VCStatus status = VCStatus.NotVersionControlled
    )
    {
        PathToEntry = path;
        IsDirectory = isDirectory;
        Icon = isDirectory ? _folderIcon : GetIcon(fileOpsHandler, path);
        Name = Path.GetFileName(path);
        LastModTime = fileOpsHandler.GetFileLastModified(path) ?? DateTime.MaxValue;
        LastModified = GetLastModified(fileOpsHandler);
        SizeB = isDirectory ? null : fileOpsHandler.GetFileSizeB(path);
        Size = SizeB is long NotNullSize ? GetSizeString(NotNullSize) : string.Empty;

        if (isDirectory)
            Type = "Directory";
        else
        {
            string extension = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            Type = extension == string.Empty ? "File" : extension + " File";
        }

        Opacity =
            fileOpsHandler.IsHidden(path, isDirectory)
            || (FileSurferSettings.TreatDotFilesAsHidden && Name.StartsWith('.'))
                ? 0.45
                : 1;

        VersionControlled = status is not VCStatus.NotVersionControlled;
        Staged = status is VCStatus.Staged;
        IsArchived = ArchiveManager.IsZipped(path);
    }

    public FileSystemEntry(DriveInfo drive)
    {
        PathToEntry = drive.Name;
        IsDirectory = true;
        Name = $"{drive.VolumeLabel} ({drive.Name.TrimEnd(Path.DirectorySeparatorChar)})";
        Type = "Drive";
        Icon = _driveIcon;
        LastModified = string.Empty;
        Size = GetSizeString(drive.TotalSize);
    }

    private string GetLastModified(IFileOperationsHandler fileOpsHandler)
    {
        DateTime? time = IsDirectory
            ? fileOpsHandler.GetDirLastModified(PathToEntry)
            : fileOpsHandler.GetFileLastModified(PathToEntry);

        if (time is DateTime notNullTime)
            return notNullTime.ToShortDateString() + " " + notNullTime.ToShortTimeString();

        return "Error";
    }

    private static Bitmap? GetIcon(IFileOperationsHandler fileOpsHandler, string path)
    {
        if (fileOpsHandler.GetFileIcon(path) is not System.Drawing.Bitmap bitmap)
            return null;

        using MemoryStream stream = new();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        return new Bitmap(stream);
    }

    public static string GetSizeString(long sizeInB)
    {
        long size = sizeInB;
        foreach (string notation in _byteUnits)
        {
            if (size <= SizeLimit)
                return size.ToString() + " " + notation;
            else
                size = (size + 1023) / 1024;
        }
        return (size * 1024).ToString() + " " + _byteUnits[^1];
    }
}
