using System;
using System.Drawing.Imaging;
using System.IO;
using FileSurfer.Models;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace FileSurfer;

/// <summary>
/// Represents a displayable file system entry (file, directory, or drive) in the FileSurfer application.
/// This class manages the properties and behaviors associated with files and directories, such as
/// their name, size, type, last modification time, and icon. It also accounts for special conditions
/// like hidden files, version control status, and archive detection within the context of the FileSurfer app.
/// </summary>
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
    public Bitmap? Icon { get; set; }
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

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemEntry"/> class for a file or directory.
    /// <para>
    /// Sets up various properties like the name, icon, size, type, and last modified date based on
    /// the provided path and version control status.
    /// </para>
    /// <para>
    /// Also handles specific conditions such as hidden files and archive detection within the
    /// context of FileSurfer.
    /// </para>
    /// </summary>
    /// <param name="fileIOHandler">Handler for file operations like retrieving file size and modification time.</param>
    /// <param name="path">The file or directory path associated with this entry.</param>
    /// <param name="isDirectory">Indicates whether the path refers to a directory.</param>
    /// <param name="status">Optional version control status of the entry, defaulting to not version controlled.</param>
    public FileSystemEntry(
        IFileIOHandler fileIOHandler,
        string path,
        bool isDirectory,
        VCStatus status = VCStatus.NotVersionControlled
    )
    {
        PathToEntry = path;
        IsDirectory = isDirectory;
        if (IsDirectory)
            Icon = _folderIcon;
        else
            SetIcon(fileIOHandler, path);

        Name = Path.GetFileName(path);
        LastModTime = fileIOHandler.GetFileLastModified(path) ?? DateTime.MaxValue;
        LastModified = GetLastModified(fileIOHandler);
        SizeB = isDirectory ? null : fileIOHandler.GetFileSizeB(path);
        Size = SizeB is long NotNullSize ? GetSizeString(NotNullSize) : string.Empty;

        if (isDirectory)
            Type = "Directory";
        else
        {
            string extension = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            Type = extension == string.Empty ? "File" : extension + " File";
        }

        Opacity =
            fileIOHandler.IsHidden(path, isDirectory)
            || (FileSurferSettings.TreatDotFilesAsHidden && Name.StartsWith('.'))
                ? 0.45
                : 1;

        VersionControlled = status is not VCStatus.NotVersionControlled;
        Staged = status is VCStatus.Staged;
        IsArchived = ArchiveManager.IsZipped(path);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemEntry"/> class for a drive.
    /// <para>
    /// Configures the properties such as name, type, icon, and total size based on the provided
    /// <see cref="DriveInfo"/> object.
    /// </para>
    /// <para>
    /// This constructor is specifically used for representing drives within the FileSurfer application.
    /// </para>
    /// </summary>
    /// <param name="drive">The drive information associated with this entry.</param>
    public FileSystemEntry(DriveInfo drive)
    {
        PathToEntry = drive.Name;
        IsDirectory = true;
        Name =
            !string.IsNullOrEmpty(drive.VolumeLabel)
                ? $"{drive.VolumeLabel} ({drive.Name.TrimEnd(Path.DirectorySeparatorChar)})"
                : drive.Name.TrimEnd(Path.DirectorySeparatorChar);

        Type = "Drive";
        Icon = _driveIcon;
        LastModified = string.Empty;
        Size = GetSizeString(drive.TotalSize);
    }

    private string GetLastModified(IFileIOHandler fileOpsHandler)
    {
        DateTime? time = IsDirectory
            ? fileOpsHandler.GetDirLastModified(PathToEntry)
            : fileOpsHandler.GetFileLastModified(PathToEntry);

        if (time is DateTime notNullTime)
            return notNullTime.ToShortDateString() + " " + notNullTime.ToShortTimeString();

        return "Error";
    }

    private void SetIcon(IFileIOHandler fileOpsHandler, string path)
    {
        using System.Drawing.Bitmap? bitmap = fileOpsHandler.GetFileIcon(path);
        if (bitmap is null)
            return;

        using MemoryStream stream = new();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        Icon = new Bitmap(stream);
    }

    /// <summary>
    /// Converts file size in bytes to a human readable format.
    /// </summary>
    /// <param name="sizeInB">Size of the file in bytes</param>
    /// <returns></returns>
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
