using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using FileSurfer.Models;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace FileSurfer;

/// <summary>
/// Represents a displayable file system entry (file, directory, or drive) in the FileSurfer application.
/// Manages properties associated with files and directories, such as
/// their name, size, type, last modification time, and icon. Also includes data about special conditions
/// like hidden files, or version control status.
/// </summary>
public class FileSystemEntry
{
    private static readonly int SizeLimit = FileSurferSettings.FileSizeDisplayLimit;
    private static readonly Bitmap FolderIcon =
        new(
            Avalonia.Platform.AssetLoader.Open(new Uri("avares://FileSurfer/Assets/FolderIcon.png"))
        );
    private static readonly Bitmap DriveIcon =
        new(
            Avalonia.Platform.AssetLoader.Open(new Uri("avares://FileSurfer/Assets/DriveIcon.png"))
        );
    private static readonly IReadOnlyList<string> ByteUnits = ["B", "KiB", "MiB", "GiB", "TiB", "PiB"];

    /// <summary>
    /// Path to the file, directory, or drive represented by this <see cref="FileSystemEntry"/>.
    /// </summary>
    public readonly string PathToEntry;

    /// <summary>
    /// Specifies if this <see cref="FileSystemEntry"/> is a directory.
    /// </summary>
    public bool IsDirectory { get; }

    /// <summary>
    /// Holds a <see cref="Avalonia.Media.Imaging.Bitmap"/> representing the file.
    /// </summary>
    public Bitmap? Icon { get; set; }

    /// <summary>
    /// Holds the name of file, directory, or drive represented by this <see cref="FileSystemEntry"/>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Holds the <see cref="DateTime"/> of this entry's last modification date.
    /// </summary>
    public DateTime LastModTime { get; }

    /// <summary>
    /// Holds this entry's last modification date as <see cref="string"/>.
    /// </summary>
    public string LastModified { get; }

    /// <summary>
    /// Holds this entry's size in bytes.
    /// </summary>
    public long? SizeB { get; }

    /// <summary>
    /// Holds this entry's size as human-readable <see cref="string"/>.
    /// </summary>
    public string Size { get; }

    /// <summary>
    /// Holds this entry's type in the context of <see cref="FileSurfer"/>.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Holds this <see cref="FileSystemEntry"/>'s opacity in the context of <see cref="Views.MainWindow"/>.
    /// </summary>
    public double Opacity { get; } = 1;

    /// <summary>
    /// Specifies if the file represented by this <see cref="FileSystemEntry"/> is part of a repository.
    /// </summary>
    public bool VersionControlled { get; } = false;

    /// <summary>
    /// Specifies if the file represented by this <see cref="FileSystemEntry"/> has been staged for the next commit.
    /// </summary>
    public bool Staged { get; } = false;

    /// <summary>
    /// Specifies if the file is in an archived format supported by <see cref="FileSurfer"/>.
    /// </summary>
    public bool IsArchived { get; } = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemEntry"/> class for a file or directory.
    /// <para>
    /// Sets up properties like the name, icon, size, type, and last modified date based on
    /// the provided path and version control status.
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
            Icon = FolderIcon;
        else
            SetIcon(fileIOHandler, path);

        Name = Path.GetFileName(path);
        LastModTime = fileIOHandler.GetFileLastModified(path) ?? DateTime.MaxValue;
        LastModified = GetLastModified(fileIOHandler);
        SizeB = isDirectory ? null : fileIOHandler.GetFileSizeB(path);
        Size = SizeB is long notNullSize ? GetSizeString(notNullSize) : string.Empty;

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
    /// This constructor is specifically used for representing drives within the <see cref="FileSurfer"/> app.
    /// </para>
    /// </summary>
    /// <param name="drive">The drive information associated with this entry.</param>
    public FileSystemEntry(DriveInfo drive)
    {
        PathToEntry = drive.Name;
        IsDirectory = true;
        Name = !string.IsNullOrEmpty(drive.VolumeLabel)
            ? $"{drive.VolumeLabel} ({drive.Name.TrimEnd(Path.DirectorySeparatorChar)})"
            : drive.Name.TrimEnd(Path.DirectorySeparatorChar);

        Type = "Drive";
        Icon = DriveIcon;
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
    /// Converts file size in bytes to a human-readable format.
    /// </summary>
    /// <param name="sizeInB">Size of the file in bytes</param>
    /// <returns>Human-readable file size as <see cref="string"/>.</returns>
    public static string GetSizeString(long sizeInB)
    {
        long size = sizeInB;
        foreach (string notation in ByteUnits)
        {
            if (size <= SizeLimit)
                return size.ToString() + " " + notation;

            size = (size + 1023) / 1024;
        }
        return (size * 1024).ToString() + " " + ByteUnits[^1];
    }
}
