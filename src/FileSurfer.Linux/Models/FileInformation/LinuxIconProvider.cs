using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.Shell;
using MimeDetective;
using MimeDetective.Engine;

namespace FileSurfer.Linux.Models.FileInformation;

public sealed class LinuxIconProvider : BaseIconProvider
{
    private const string GenericMimeType = "unknown";
    private const int SvgSize = 128;
    private static readonly Task<IContentInspector> MimeInspectorTask = Task.Run(() =>
        new ContentInspectorBuilder
        {
            Definitions = new MimeDetective.Definitions.ExhaustiveBuilder
            {
                UsageType = MimeDetective.Definitions.Licensing.UsageType.PersonalNonCommercial,
            }.Build(),
        }.Build()
    );

    private readonly IShellHandler _shellHandler;
    private readonly IReadOnlyList<string> _searchPaths;
    private readonly ConcurrentDictionary<string, string> _extToMime = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<Bitmap>>> _mimeToIcon = new();
    private readonly Bitmap _themedGenericFileIcon;

    private static string NormalizeMime(string mime) => mime.ToLowerInvariant().Replace('/', '-');

    public LinuxIconProvider(IShellHandler shellHandler)
    {
        _shellHandler = shellHandler;
        _searchPaths = IconPathResolver.GetSearchPaths(shellHandler);
        _themedGenericFileIcon =
            ExtractIcon(GenericMimeType) ?? base.GetFileIconAsync(string.Empty).Result;

        if (!File.Exists(GlobsParser.GlobsPath))
            return;
        try
        {
            using StreamReader reader = File.OpenText(GlobsParser.GlobsPath);
            _extToMime = new ConcurrentDictionary<string, string>(GlobsParser.Parse(reader));
        }
        catch
        {
            // Parsing failed, continuing without _extToMime
        }
    }

    public override async Task<Bitmap> GetFileIconAsync(string filePath)
    {
        string mimeType = await GetMimeTypeAsync(filePath);

        Lazy<Task<Bitmap>> lazy = _mimeToIcon.GetOrAdd(
            mimeType,
            m => new Lazy<Task<Bitmap>>(
                () => Task.Run(() => ExtractIcon(m) ?? _themedGenericFileIcon),
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );

        return await lazy.Value;
    }

    private async Task<string> GetMimeTypeAsync(string filePath)
    {
        string? extension = null;
        foreach (string ext in LocalPathTools.EnumerateExtensions(filePath))
        {
            extension = ext;
            if (_extToMime.TryGetValue(ext, out string? mime))
                return mime;
        }

        string mimeType;
        try
        {
            IContentInspector inspector = await MimeInspectorTask;
            ImmutableArray<MimeTypeMatch> result = await Task.Run(() =>
                inspector.Inspect(filePath).ByMimeType()
            );
            mimeType = result.IsEmpty
                ? await GetXdgMimeTypeAsync(filePath)
                : NormalizeMime(result[0].MimeType);
        }
        catch
        {
            mimeType = GenericMimeType;
        }
        if (!string.IsNullOrEmpty(extension) && mimeType != GenericMimeType)
            _extToMime.TryAdd(extension, mimeType);

        return mimeType;
    }

    private async Task<string> GetXdgMimeTypeAsync(string filePath)
    {
        ValueResult<string> result = await _shellHandler.ExecuteCommandAsync(
            "xdg-mime",
            "query",
            "filetype",
            filePath
        );
        if (!result.IsOk || string.IsNullOrEmpty(result.Value))
            return GenericMimeType;

        return NormalizeMime(result.Value);
    }

    private Bitmap? ExtractIcon(string mimeType)
    {
        foreach (string path in _searchPaths)
        {
            string svgPath = Path.Combine(path, mimeType + ".svg");
            if (File.Exists(svgPath))
                return SvgHelper.RenderSvg(svgPath, SvgSize);

            string pngPath = Path.Combine(path, mimeType + ".png");
            if (File.Exists(pngPath))
                return new Bitmap(pngPath);
        }
        return null;
    }

    public override void Dispose()
    {
        foreach (Lazy<Task<Bitmap>> task in _mimeToIcon.Values)
            if (task.Value.IsCompletedSuccessfully)
                task.Value.Result.Dispose();

        _mimeToIcon.Clear();
        _themedGenericFileIcon.Dispose();
        base.Dispose();
    }
}
