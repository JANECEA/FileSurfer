using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;

namespace FileSurfer.Models.FileInformation;

/// <summary>
/// Optimizes icon delivery based on the file extension.
/// </summary>
public class IconProvider : IIconProvider, IDisposable
{
    private static readonly Bitmap DirectoryIcon =
        new(
            Avalonia.Platform.AssetLoader.Open(new Uri("avares://FileSurfer/Assets/FolderIcon.png"))
        );
    private static readonly Bitmap DriveIcon =
        new(
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
        string extension = Path.GetExtension(filePath);
        if (HaveUniqueIcons.Contains(extension))
            return _fileInfoProvider.GetFileIcon(filePath)?.ConvertToAvaloniaBitmap()
                ?? _genericFileIcon;

        if (string.IsNullOrWhiteSpace(extension))
            return _genericFileIcon ??= GetGenericFileIcon(filePath);

        if (_icons.TryGetValue(filePath, out Bitmap? cachedIcon))
            return cachedIcon;

        if (_fileInfoProvider.GetFileIcon(filePath)?.ConvertToAvaloniaBitmap() is Bitmap icon)
            return _icons[extension] = icon;

        return _genericFileIcon;
    }

    /// <inheritdoc/>
    public Bitmap GetDirectoryIcon(string dirPath) => DirectoryIcon;

    /// <inheritdoc/>
    public Bitmap GetDriveIcon(DriveInfo driveInfo) => DriveIcon;

    private Bitmap? GetGenericFileIcon(string genericFilePath) =>
        _fileInfoProvider.GetFileIcon(genericFilePath)?.ConvertToAvaloniaBitmap();

    public void Dispose()
    {
        foreach (var icon in _icons.Values)
            icon.Dispose();

        _icons.Clear();
    }
}

public static class BitmapExtensions
{
    public static Bitmap ConvertToAvaloniaBitmap(this System.Drawing.Bitmap bitmap)
    {
        using MemoryStream stream = new();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        return new Bitmap(stream);
    }
}
