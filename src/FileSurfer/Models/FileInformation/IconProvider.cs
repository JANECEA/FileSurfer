using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using FileSurfer.Models.FileOperations;

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
    private static Bitmap? GenericFileIcon;

    private readonly IFileIOHandler _fileIOHandler;
    private readonly Dictionary<string, Bitmap> _icons = new();

    public IconProvider(IFileIOHandler fileIOHandler)
    {
        _fileIOHandler = fileIOHandler;
    }

    /// <inheritdoc/>
    public Bitmap? GetFileIcon(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        if (HaveUniqueIcons.Contains(extension))
            return _fileIOHandler.GetFileIcon(filePath)?.ConvertToAvaloniaBitmap()
                ?? GenericFileIcon;

        if (string.IsNullOrWhiteSpace(extension))
            return GenericFileIcon ??= GetGenericFileIcon(filePath);

        if (_icons.TryGetValue(filePath, out Bitmap? cachedIcon))
            return cachedIcon;

        if (_fileIOHandler.GetFileIcon(filePath)?.ConvertToAvaloniaBitmap() is Bitmap icon)
            return _icons[extension] = icon;

        return GenericFileIcon;
    }

    /// <inheritdoc/>
    public Bitmap GetDirectoryIcon() => DirectoryIcon;

    /// <inheritdoc/>
    public Bitmap GetDriveIcon() => DriveIcon;

    private Bitmap? GetGenericFileIcon(string genericFilePath) =>
        _fileIOHandler.GetFileIcon(genericFilePath)?.ConvertToAvaloniaBitmap();

    public void Dispose()
    {
        foreach (var icon in _icons.Values)
            icon.Dispose();

        _icons.Clear();
    }
}
