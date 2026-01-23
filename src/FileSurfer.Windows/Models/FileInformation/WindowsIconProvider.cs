using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Windows.Models.FileInformation;

/// <summary>
/// Optimizes Windows icon delivery based on the file extension.
/// </summary>
public sealed class WindowsIconProvider : IIconProvider
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

    private readonly ConcurrentDictionary<string, Bitmap> _icons = new();
    private readonly object _genericIconLock = new();
    private Bitmap? _genericFileIcon;

    private Bitmap GetGenericFileIcon(string filePath)
    {
        if (_genericFileIcon is not null)
            return _genericFileIcon;

        lock (_genericIconLock)
        {
            if (_genericFileIcon is not null) // double-checked locking
                return _genericFileIcon;

            _genericFileIcon = ExtractFileIcon(filePath);
            return _genericFileIcon ?? GenericFileIcon;
        }
    }

    public async Task<Bitmap> GetFileIcon(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
            return GetGenericFileIcon(filePath);

        if (HaveUniqueIcons.Contains(extension))
            return await Task.Run(() => ExtractFileIcon(filePath) ?? GetGenericFileIcon(filePath));

        if (_icons.TryGetValue(extension, out Bitmap? cachedIcon))
            return cachedIcon;

        Bitmap? icon = await Task.Run(() => ExtractFileIcon(filePath));
        if (icon is null)
            return GetGenericFileIcon(filePath);

        _icons.TryAdd(extension, icon);
        return icon;
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

    public Task<Bitmap> GetDirectoryIcon(string dirPath) => Task.FromResult(DirectoryIcon);

    public Task<Bitmap> GetDriveIcon(DriveEntry driveEntry) => Task.FromResult(DriveIcon);

    public void Dispose()
    {
        foreach (Bitmap icon in _icons.Values)
            icon.Dispose();

        _genericFileIcon?.Dispose();
    }
}
