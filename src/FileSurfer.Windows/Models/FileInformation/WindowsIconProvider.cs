using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Windows.Models.FileInformation;

/// <summary>
/// Optimizes Windows icon delivery based on the file extension.
/// </summary>
public class WindowsIconProvider : IIconProvider, IDisposable
{
    private static readonly SvgImage DirectoryIcon = new()
    {
        Source = SvgSource.LoadFromStream(
            AssetLoader.Open(new Uri("avares://FileSurfer.Core/Assets/FolderIcon.svg"))
        ),
    };
    private static readonly SvgImage DriveIcon = new()
    {
        Source = SvgSource.LoadFromStream(
            AssetLoader.Open(new Uri("avares://FileSurfer.Core/Assets/DriveIcon.svg"))
        ),
    };
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

    private readonly Dictionary<string, Bitmap> _icons = new();
    private Bitmap? _genericFileIcon;

    /// <inheritdoc/>
    public IImage? GetFileIcon(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
            return _genericFileIcon ??= ExtractFileIcon(filePath);

        if (HaveUniqueIcons.Contains(extension))
            return ExtractFileIcon(filePath) ?? _genericFileIcon;

        if (_icons.TryGetValue(extension, out Bitmap? cachedIcon))
            return cachedIcon;

        if (ExtractFileIcon(filePath) is Bitmap icon)
            return _icons[extension] = icon;

        return _genericFileIcon;
    }

    private static Bitmap? ExtractFileIcon(string path)
    {
        try
        {
            using System.Drawing.Icon? icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null)
                return null;

            using System.Drawing.Bitmap winBitmap = icon.ToBitmap();
            using MemoryStream memoryStream = new();
            winBitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
            memoryStream.Position = 0;
            Bitmap bitmap = new(memoryStream);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public IImage GetDirectoryIcon(string dirPath) => DirectoryIcon;

    /// <inheritdoc/>
    public IImage GetDriveIcon(DriveEntry driveEntry) => DriveIcon;

    public void Dispose()
    {
        _genericFileIcon?.Dispose();
        foreach (Bitmap icon in _icons.Values)
            icon.Dispose();

        _icons.Clear();
    }
}
