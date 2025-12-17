using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
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
    private readonly Dictionary<string, IImage> _icons = new();
    private IImage? _genericFileIcon;

    /// <inheritdoc/>
    public IImage? GetFileIcon(string filePath)
    {
        string mimeType = GetMimeType(filePath);
        IImage? icon = LoadIcon(mimeType);
        return icon;
    }

    private string GetMimeType(string filePath)
    {
        return "application-pdf";
    }

    IImage? LoadIcon(string mimeType)
    {
        string[] searchPaths =
        {
            "/usr/share/icons/hicolor/mimetypes/64",
            "/usr/share/icons/breeze-dark/mimetypes/64",
            "/usr/share/icons/breeze/mimetypes/64",
        };

        foreach (string path in searchPaths)
        {
            string svgPath = Path.Combine(path, mimeType + ".svg");
            if (File.Exists(svgPath))
                return new SvgImage { Source = SvgSource.Load(svgPath) };

            string pngPath = Path.Combine(path, mimeType + ".png");
            if (File.Exists(pngPath))
                return new Bitmap(pngPath);
        }
        return null;
    }

    /// <inheritdoc/>
    public IImage GetDirectoryIcon(string dirPath) => DirectoryIcon;

    /// <inheritdoc/>
    public IImage GetDriveIcon(DriveEntry driveEntry) => DriveIcon;

    public void Dispose()
    {
        DirectoryIcon.Dispose();
        DriveIcon.Dispose();
        _icons.Clear();
    }
}
