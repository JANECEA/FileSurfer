using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.VersionControl;
using ReactiveUI;

namespace FileSurfer.Core.ViewModels;

/// <summary>
/// Represents a displayable file system entry (file, directory, or drive) in the FileSurfer application.
/// Manages properties associated with files and directories, such as
/// their name, size, type, last modification time, and icon. Also includes data about special conditions
/// like hidden files, or version control status.
/// </summary>
[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Properties are used by the window"
)]
public sealed class FileSystemEntryViewModel : ReactiveObject
{
    private static readonly IReadOnlyList<string> ByteUnits =
    [
        "B",
        "KiB",
        "MiB",
        "GiB",
        "TiB",
        "PiB",
    ];

    /// <summary>
    /// Path to the file, directory, or drive represented by this <see cref="FileSystemEntryViewModel"/>.
    /// </summary>
    public string PathToEntry => FileSystemEntry.PathToEntry;

    /// <summary>
    /// Specifies if this <see cref="FileSystemEntryViewModel"/> is a directory.
    /// </summary>
    public bool IsDirectory => FileSystemEntry is DirectoryEntry or DriveEntry;

    /// <summary>
    /// Holds a <see cref="Bitmap"/> representing the file.
    /// </summary>
    public Bitmap? Icon
    {
        get => _icon;
        private set => this.RaiseAndSetIfChanged(ref _icon, value);
    }
    private Bitmap? _icon = null;

    /// <summary>
    /// Holds the name of file, directory, or drive represented by this <see cref="FileSystemEntryViewModel"/>.
    /// </summary>
    public string Name => FileSystemEntry.Name;

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
    /// Holds this <see cref="FileSystemEntryViewModel"/>'s opacity in the context of <see cref="Views.MainWindow"/>.
    /// </summary>
    public double Opacity { get; } = 1;

    /// <summary>
    /// Specifies if the file represented by this <see cref="FileSystemEntryViewModel"/> is part of a repository.
    /// </summary>
    public bool VersionControlled
    {
        get => _versionControlled;
        private set => this.RaiseAndSetIfChanged(ref _versionControlled, value);
    }
    private bool _versionControlled;

    /// <summary>
    /// Specifies if the file represented by this <see cref="FileSystemEntryViewModel"/> has been staged for the next commit.
    /// </summary>
    public bool Staged
    {
        get => _staged;
        private set => this.RaiseAndSetIfChanged(ref _staged, value);
    }
    private bool _staged;

    /// <summary>
    /// Specifies if the file is in an archived format supported by <see cref="FileSurfer"/>.
    /// </summary>
    public bool IsArchived { get; } = false;

    /// <summary>
    /// Specifies if the open as dialog can be invoked on this entry
    /// </summary>
    public bool SupportsOpenAs { get; }

    /// <summary>
    /// Holds the underlying <see cref="IFileSystemEntry"/>.
    /// </summary>
    public IFileSystemEntry FileSystemEntry { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemEntryViewModel"/> class for a file or directory.
    /// <para>
    /// Sets up properties like the name, icon, size, type, and last modified date based on
    /// the provided path and version control status.
    /// </para>
    /// </summary>
    /// <param name="fileSystem">Current fileSystem</param>
    /// <param name="entry">The file or directory entry.</param>
    /// <param name="status">Optional version control status of the entry, defaulting to not version controlled.</param>
    public FileSystemEntryViewModel(
        IFileSystem fileSystem,
        IFileSystemEntry entry,
        GitStatus status = GitStatus.NotVersionControlled
    )
    {
        FileSystemEntry = entry;
        _ = LoadIconAsync(entry, fileSystem.IconProvider);

        LastModTime =
            fileSystem.FileInfoProvider.GetFileLastModified(entry.PathToEntry) ?? DateTime.MaxValue;
        LastModified = GetLastModified(fileSystem.FileInfoProvider);
        SizeB = IsDirectory ? null : fileSystem.FileInfoProvider.GetFileSizeB(entry.PathToEntry);
        Size = SizeB is long notNullSize ? GetSizeString(notNullSize) : string.Empty;

        if (IsDirectory)
            Type = "Directory";
        else
        {
            string extension = entry.Extension.TrimStart('.').ToUpperInvariant();
            Type = string.IsNullOrEmpty(extension) ? "File" : extension + " File";
        }

        Opacity = fileSystem.FileInfoProvider.IsHidden(entry.PathToEntry, IsDirectory) ? 0.5 : 1;

        UpdateVcStatus(status);
        IsArchived = fileSystem.ArchiveManager.IsZipped(entry.PathToEntry);
        SupportsOpenAs = fileSystem.FileProperties.SupportsOpenAs(entry);
    }

    private async Task LoadIconAsync(IFileSystemEntry entry, IIconProvider iconProvider) =>
        Icon = entry switch
        {
            FileEntry => await iconProvider.GetFileIcon(entry.PathToEntry),
            DirectoryEntry => await iconProvider.GetDirectoryIcon(entry.PathToEntry),
            DriveEntry driveEntry => await iconProvider.GetDriveIcon(driveEntry),
            _ => throw new NotSupportedException(),
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemEntryViewModel"/> class for a drive.
    /// <para>
    /// Configures the properties such as name, type, icon, and total size based on the provided
    /// <see cref="DriveEntry"/> object.
    /// </para>
    /// <para>
    /// This constructor is specifically used for representing drives within the <see cref="FileSurfer"/> app.
    /// </para>
    /// </summary>
    public FileSystemEntryViewModel(IFileSystem fileSystem, DriveEntry driveEntry)
    {
        FileSystemEntry = driveEntry;
        Type = "Drive";
        _ = LoadIconAsync(driveEntry, fileSystem.IconProvider);
        LastModified = string.Empty;
        Size = GetSizeString(driveEntry.SizeB);
        SupportsOpenAs = fileSystem.FileProperties.SupportsOpenAs(driveEntry);
    }

    internal void UpdateVcStatus(GitStatus newStatus)
    {
        VersionControlled = newStatus is not GitStatus.NotVersionControlled;
        Staged = newStatus is GitStatus.Staged;
    }

    private string GetLastModified(IFileInfoProvider fileInfoProvider)
    {
        DateTime? time = IsDirectory
            ? fileInfoProvider.GetDirLastModified(PathToEntry)
            : fileInfoProvider.GetFileLastModified(PathToEntry);

        if (time is DateTime notNullTime)
            return notNullTime.ToShortDateString() + " " + notNullTime.ToShortTimeString();

        return "Error";
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
            if (size <= FileSurferSettings.FileSizeUnitLimit)
                return $"{size} {notation}";

            size = (size + 1023) / 1024;
        }
        return $"{size * 1024} {ByteUnits[^1]}";
    }
}
