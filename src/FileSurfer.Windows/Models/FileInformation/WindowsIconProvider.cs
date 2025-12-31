using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Windows.Models.FileInformation;

/// <summary>
/// Optimizes Windows icon delivery based on the file extension.
/// </summary>
public class WindowsIconProvider : IIconProvider, IDisposable
{
    private static readonly Bitmap GenericFileIcon = new(
        AssetLoader.Open(new Uri("avares://FileSurfer.Core/Assets/GenericFileIcon.png"))
    );
    private static readonly Bitmap DirectoryIcon = new(
        AssetLoader.Open(new Uri("avares://FileSurfer.Core/Assets/FolderIcon.png"))
    );
    private static readonly Bitmap DriveIcon = new(
        AssetLoader.Open(new Uri("avares://FileSurfer.Core/Assets/DriveIcon.png"))
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

    private readonly Dictionary<string, Bitmap> _icons = new();
    private Bitmap? _genericFileIcon;

    private Bitmap GetGenericFileIcon() => _genericFileIcon ?? GenericFileIcon;

    public Bitmap GetFileIcon(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            _genericFileIcon ??= ExtractFileIcon(filePath);
            return GetGenericFileIcon();
        }

        if (HaveUniqueIcons.Contains(extension))
            return ExtractFileIcon(filePath) ?? GetGenericFileIcon();

        if (_icons.TryGetValue(extension, out Bitmap? cachedIcon))
            return cachedIcon;

        if (ExtractFileIcon(filePath) is Bitmap icon)
            return _icons[extension] = icon;

        return GetGenericFileIcon();
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

    public Bitmap GetDirectoryIcon(string dirPath) => DirectoryIcon;

    public Bitmap GetDriveIcon(DriveEntry driveEntry) => DriveIcon;

    public void Dispose()
    {
        _genericFileIcon?.Dispose();
        foreach (Bitmap icon in _icons.Values)
            icon.Dispose();

        _icons.Clear();
    }
}
