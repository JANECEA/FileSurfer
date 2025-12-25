using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using MimeDetective;
using MimeDetective.Engine;

namespace FileSurfer.Linux.Models.FileInformation;

public class LinuxIconProvider : IIconProvider, IDisposable
{
    private const string GenericMimeType = "unknown";
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
    private static readonly Task<IContentInspector> MimeInspectorTask = Task.Run(() =>
        new ContentInspectorBuilder
        {
            Definitions = new MimeDetective.Definitions.ExhaustiveBuilder
            {
                UsageType = MimeDetective.Definitions.Licensing.UsageType.PersonalNonCommercial,
            }.Build(),
        }.Build()
    );

    private readonly IReadOnlyList<string> _searchPaths;
    private readonly Dictionary<string, string> _extToMime = new();
    private readonly Dictionary<string, IImage> _mimeToIcon = new();
    private IImage? _genericFileIcon;

    private IImage? GetGenericFileIcon() => _genericFileIcon ??= ExtractIcon(GenericMimeType);

    public LinuxIconProvider()
    {
        _searchPaths = IconPathResolver.GetSearchPaths();

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
        string? extension = null;
        foreach (string ext in PathTools.EnumerateExtensions(filePath))
        {
            extension = ext;
            if (_extToMime.TryGetValue(ext, out string? mime))
                return mime;
        }

        string mimeType;
        try
        {
            ImmutableArray<MimeTypeMatch> result = MimeInspectorTask.IsCompleted
                ? MimeInspectorTask.Result.Inspect(filePath).ByMimeType()
                : new ImmutableArray<MimeTypeMatch>();

            mimeType = result.IsEmpty
                ? GetXdgMimeType(filePath)
                : result[0].MimeType.ToLowerInvariant().Replace('/', '-');
        }
        catch
        {
            mimeType = GenericMimeType;
        }
        if (!string.IsNullOrEmpty(extension) && mimeType != GenericMimeType)
            _extToMime.Add(extension, mimeType);

        return mimeType;
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

    private IImage? ExtractIcon(string mimeType)
    {
        foreach (string path in _searchPaths)
        {
            string svgPath = Path.Combine(path, mimeType + ".svg");
            // TODO fix icon transparency
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
        _extToMime.Clear();
        _mimeToIcon.Clear();
    }
}
