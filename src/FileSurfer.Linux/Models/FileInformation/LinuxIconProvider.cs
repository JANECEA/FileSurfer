using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using MimeDetective;
using MimeDetective.Engine;

namespace FileSurfer.Linux.Models.FileInformation;

/// <summary>
/// Optimizes Windows icon delivery based on the file extension.
/// </summary>
public class LinuxIconProvider : IIconProvider, IDisposable
{
    private const string GenericMimeType = "unknown";
    private static readonly IReadOnlyList<string> SearchPaths =
    [
        "/usr/share/icons/hicolor/64x64/mimetypes",
        "/usr/share/icons/breeze-dark/mimetypes/64",
        "/usr/share/icons/breeze-dark/mimetypes/32",
        "/usr/share/icons/breeze/mimetypes/64",
        "/usr/share/icons/breeze/mimetypes/32",
    ];
    private static readonly Bitmap DirectoryIcon = new(
        Avalonia.Platform.AssetLoader.Open(
            new Uri("avares://FileSurfer.Core/Assets/FolderIcon.png")
        )
    );
    private static readonly Bitmap DriveIcon = new(
        Avalonia.Platform.AssetLoader.Open(new Uri("avares://FileSurfer.Core/Assets/DriveIcon.png"))
    );
    private static readonly IContentInspector MimeInspector = new ContentInspectorBuilder
    {
        Definitions = MimeDetective.Definitions.DefaultDefinitions.All(),
    }.Build();

    private readonly Dictionary<string, string> _extToMime = new();
    private readonly Dictionary<string, IImage> _mimeToIcon = new();
    private IImage? _genericFileIcon;

    private IImage? GetGenericFileIcon() => _genericFileIcon ??= ExtractIcon(GenericMimeType);

    public LinuxIconProvider()
    {
        if (!File.Exists(GlobsParser.GlobsPath))
            return;

        using StreamReader reader = File.OpenText(GlobsParser.GlobsPath);
        _extToMime = GlobsParser.Parse(reader);
    }

    /// <inheritdoc/>
    public IImage? GetFileIcon(string filePath)
    {
        string mimeType = GetMimeType(filePath);
        if (!_mimeToIcon.TryGetValue(mimeType, out IImage? icon))
        {
            icon = ExtractIcon(mimeType);
            if (icon is not null)
                _mimeToIcon[mimeType] = icon;
        }
        return icon ?? GetGenericFileIcon();
    }

    private string GetMimeType(string filePath)
    {
        foreach (string extension in PathTools.EnumerateExtensions(filePath))
            if (_extToMime.TryGetValue(extension, out string? mime))
                return mime;

        ImmutableArray<FileExtensionMatch> result = MimeInspector
            .Inspect(filePath)
            .ByFileExtension();

        if (result.Length > 0 && result[0].Matches[^1].Definition.File.MimeType is string mimeType)
            return mimeType.ToLowerInvariant().Replace('/', '-');

        return GenericMimeType;
    }

    private static IImage? ExtractIcon(string mimeType)
    {
        foreach (string path in SearchPaths)
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
        _extToMime.Clear();
        _mimeToIcon.Clear();
    }
}

internal static class GlobsParser
{
    internal const string GlobsPath = "/usr/share/mime/globs";

    internal static Dictionary<string, string> Parse(StreamReader reader)
    {
        Dictionary<string, string> result = new();

        while (reader.ReadLine() is { } line)
            if (line.Length != 0 && line[0] != '#' && GetExtension(line) is { } extension)
                _ = result.TryAdd(extension, GetMimeType(line));

        return result;
    }

    private static string? GetExtension(string str)
    {
        int i = str.Length - 1;
        for (; i >= 0; i--)
            if (str[i] == '*')
                break;

        if (i < 0 || i >= str.Length - 3)
            return null;

        return str[(i + 2)..];
    }

    private static string GetMimeType(string str)
    {
        int i = str.Length - 1;
        for (; i >= 0; i--)
            if (str[i] == ':')
                break;

        return str[..i].Replace('/', '-');
    }
}
