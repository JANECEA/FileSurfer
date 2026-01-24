using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
    private readonly IconWorker _iconWorker = new();
    private readonly object _genericIconLock = new();
    private Bitmap? _genericFileIcon = null;

    private async Task<Bitmap> GetGenericFileIcon(string filePath)
    {
        if (_genericFileIcon is not null)
            return _genericFileIcon;

        Bitmap? icon = await _iconWorker.Enqueue(filePath).ConfigureAwait(false);

        if (icon is null)
            return GenericFileIcon;

        lock (_genericIconLock)
            _genericFileIcon ??= icon;

        return icon;
    }

    public async Task<Bitmap> GetFileIcon(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
            return await GetGenericFileIcon(filePath);

        if (HaveUniqueIcons.Contains(extension))
            return await CreateIconTask(filePath) ?? await GetGenericFileIcon(filePath);

        if (_icons.TryGetValue(extension, out Bitmap? cachedIcon))
            return cachedIcon;

        Bitmap? icon = await CreateIconTask(filePath);
        if (icon is null)
            return await GetGenericFileIcon(filePath);

        _icons.TryAdd(extension, icon);
        return icon;
    }

    private ConfiguredTaskAwaitable<Bitmap?> CreateIconTask(string filePath) =>
        _iconWorker.Enqueue(filePath).ConfigureAwait(false);

    public Task<Bitmap> GetDirectoryIcon(string dirPath) => Task.FromResult(DirectoryIcon);

    public Task<Bitmap> GetDriveIcon(DriveEntry driveEntry) => Task.FromResult(DriveIcon);

    public void Dispose()
    {
        foreach (Bitmap icon in _icons.Values)
            icon.Dispose();

        _genericFileIcon?.Dispose();
        _iconWorker.Dispose();
    }
}
