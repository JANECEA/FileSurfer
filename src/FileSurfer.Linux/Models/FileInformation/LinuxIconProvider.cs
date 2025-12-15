using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
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
    private static readonly IReadOnlyList<string> HaveUniqueIcons =
    [
        ".exe",
        ".lnk",
        ".url",
        ".dll",
        ".ico",
        ".msi",
        ".scr",
        ".pif",
        ".scf",
        ".ocx",
        ".cpl",
        ".cur",
        ".ani",
        ".library-ms",
        ".appref-ms",
    ];
    private static Bitmap? _genericFileIcon;

    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly Dictionary<string, Bitmap> _icons = new();

    public LinuxIconProvider(IFileInfoProvider fileInfoProvider) =>
        _fileInfoProvider = fileInfoProvider;

    /// <inheritdoc/>
    public Bitmap? GetFileIcon(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(extension))
            return _genericFileIcon ??= RetrieveFileIcon(filePath);

        if (HaveUniqueIcons.Contains(extension))
            return RetrieveFileIcon(filePath) ?? _genericFileIcon;

        if (_icons.TryGetValue(extension, out Bitmap? cachedIcon))
            return cachedIcon;

        if (RetrieveFileIcon(filePath) is Bitmap icon)
            return _icons[extension] = icon;

        return _genericFileIcon;
    }

    private Bitmap? RetrieveFileIcon(string filePath) =>
        _fileInfoProvider.TryGetFileIcon(filePath, out Bitmap? bitmap) ? bitmap : null;

    /// <inheritdoc/>
    public Bitmap GetDirectoryIcon(string dirPath) => DirectoryIcon;

    /// <inheritdoc/>
    public Bitmap GetDriveIcon(DriveInfo driveInfo) => DriveIcon;

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
