using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;

namespace FileSurfer.Models.FileInformation;

/// <summary>
/// Optimizes icon delivery based on the file extension.
/// </summary>
public class IconProvider : IIconProvider, IDisposable
{
    private static readonly Bitmap DirectoryIcon = new(
        Avalonia.Platform.AssetLoader.Open(new Uri("avares://FileSurfer/Assets/FolderIcon.png"))
    );
    private static readonly Bitmap DriveIcon = new(
        Avalonia.Platform.AssetLoader.Open(new Uri("avares://FileSurfer/Assets/DriveIcon.png"))
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

    public IconProvider(IFileInfoProvider fileInfoProvider) => _fileInfoProvider = fileInfoProvider;

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

    private Bitmap? RetrieveFileIcon(string filePath)
    {
        using MemoryStream? bitmapStream = _fileInfoProvider.GetFileIconStream(filePath);
        return bitmapStream is not null ? new Bitmap(bitmapStream) : null;
    }

    /// <inheritdoc/>
    public Bitmap GetDirectoryIcon(string dirPath) => DirectoryIcon;

    /// <inheritdoc/>
    public Bitmap GetDriveIcon(DriveInfo driveInfo) => DriveIcon;

    public void Dispose()
    {
        _genericFileIcon?.Dispose();
        DirectoryIcon.Dispose();
        DriveIcon.Dispose();

        foreach (var icon in _icons.Values)
            icon.Dispose();

        _icons.Clear();
    }
}
