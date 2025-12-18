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
    private const string GlobsPath = "/usr/share/mime/globs";
    private const string GenericMimeType = "unknown";

    private static readonly Bitmap DirectoryIcon = new(
        Avalonia.Platform.AssetLoader.Open(
            new Uri("avares://FileSurfer.Core/Assets/FolderIcon.png")
        )
    );
    private static readonly Bitmap DriveIcon = new(
        Avalonia.Platform.AssetLoader.Open(new Uri("avares://FileSurfer.Core/Assets/DriveIcon.png"))
    );
    private readonly IContentInspector _mimeInspector = new ContentInspectorBuilder
    {
        Definitions = new MimeDetective.Definitions.ExhaustiveBuilder
        {
            UsageType = MimeDetective.Definitions.Licensing.UsageType.PersonalNonCommercial,
        }.Build(),
    }.Build();
    private readonly Dictionary<string, string> _mimeTypes = new();
    private readonly Dictionary<string, IImage> _icons = new();

    private IImage? _genericFileIcon;

    private IImage? GetGenericFileIcon() => _genericFileIcon ??= LoadIcon(GenericMimeType);

    public LinuxIconProvider()
    {
        if (!File.Exists(GlobsPath))
            return;

        using StreamReader reader = File.OpenText(GlobsPath);
        _mimeTypes = GlobsParser.Parse(reader);
    }

    /// <inheritdoc/>
    public IImage? GetFileIcon(string filePath)
    {
        string mimeType = GetMimeType(filePath);
        if (!_icons.TryGetValue(mimeType, out IImage? icon))
        {
            icon = LoadIcon(mimeType);
            if (icon is not null)
                _icons[mimeType] = icon;
        }
        return icon ?? GetGenericFileIcon();
    }

    private string GetMimeType(string filePath)
    {
        if (
            GetExtension(filePath) is { } extension
            && _mimeTypes.TryGetValue(extension, out string? mime)
        )
            return mime;

        ImmutableArray<FileExtensionMatch> result = _mimeInspector
            .Inspect(filePath)
            .ByFileExtension();
        if (result.Length > 0 && result[0].Matches[^1].Definition.File.MimeType is string mimeType)
            return mimeType.ToLowerInvariant().Replace('/', '-');

        return "unknown";
    }

    private static string? GetExtension(string path)
    {
        int extensionIndex = -1;
        for (int i = path.Length - 1; i >= 0; i--)
        {
            if (path[i] == '.')
                extensionIndex = i;

            if (path[i] == '/')
                break;
        }
        if (extensionIndex < 0 || extensionIndex == path.Length - 1)
            return null;

        return path[(extensionIndex + 1)..];
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

internal static class GlobsParser
{
    internal static Dictionary<string, string> Parse(StreamReader reader)
    {
        Dictionary<string, string> result = new();
        while (reader.ReadLine() is { } line)
            if (
                line.Length != 0
                && line[0] != '#'
                && GetExtension(line) is { } extension
                && !result.ContainsKey(extension)
            )
                result[extension] = GetMimeType(line);

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
