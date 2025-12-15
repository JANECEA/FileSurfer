using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Linux.Models.FileInformation;

/// <summary>
/// Optimizes Windows icon delivery based on the file extension.
/// </summary>
public class LinuxIconProvider : IIconProvider, IDisposable
{
    private static readonly Bitmap DirectoryIcon = new(
        Avalonia.Platform.AssetLoader.Open(
            new Uri("avares://FileSurfer.Core/Assets/FolderIcon.png")
        )
    );
    private static readonly Bitmap DriveIcon = new(
        Avalonia.Platform.AssetLoader.Open(new Uri("avares://FileSurfer.Core/Assets/DriveIcon.png"))
    );
    private readonly Dictionary<string, Bitmap> _icons = new();
    private Bitmap? _genericFileIcon;

    /// <inheritdoc/>
    public Bitmap? GetFileIcon(string filePath)
    {
        return null;
    }

    /// <inheritdoc/>
    public Bitmap GetDirectoryIcon(string dirPath) => DirectoryIcon;

    /// <inheritdoc/>
    public Bitmap GetDriveIcon(DriveEntry driveEntry) => DriveIcon;

    public void Dispose()
    {
        _genericFileIcon?.Dispose();
        DirectoryIcon.Dispose();
        DriveIcon.Dispose();

        foreach (Bitmap icon in _icons.Values)
            icon.Dispose();

        _icons.Clear();
    }
}
