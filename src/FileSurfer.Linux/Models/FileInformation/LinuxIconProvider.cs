using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using MimeDetective;
using MimeDetective.Engine;

namespace FileSurfer.Linux.Models.FileInformation;

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
    private static readonly Task<IContentInspector> MimeInspectorTask = Task.Run(() =>
        new ContentInspectorBuilder
        {
            Definitions = new MimeDetective.Definitions.ExhaustiveBuilder
            {
                UsageType = MimeDetective.Definitions.Licensing.UsageType.PersonalNonCommercial,
            }.Build(),
        }.Build()
    );

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

        try
        {
            ImmutableArray<MimeTypeMatch> result = MimeInspectorTask.IsCompleted
                ? MimeInspectorTask.Result.Inspect(filePath).ByMimeType()
                : new ImmutableArray<MimeTypeMatch>();

            if (!result.IsEmpty)
                return result[0].MimeType.ToLowerInvariant().Replace('/', '-');
        }
        catch
        {
            return GenericMimeType;
        }
        return GetXdgMimeType(filePath);
    }

    // To ShellHandler
    private static string GetXdgMimeType(string filePath)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "xdg-mime",
            Arguments = $"query filetype \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process? process = Process.Start(psi);
        if (process == null)
            return GenericMimeType;

        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0 ? output.Trim().Replace('/', '-') : GenericMimeType;
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
