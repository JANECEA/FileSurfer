using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.FileOperations;
using FileSurfer.Core.Models.Shell;
using FileSurfer.Core.Models.VersionControl;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Models;

/// <summary>
/// Packages all platform specific models
/// </summary>
public interface IFileSystem
{
    public IFileInfoProvider FileInfoProvider { get; }
    public IIconProvider IconProvider { get; }
    public IClipboardManager ClipboardManager { get; }
    public IArchiveManager ArchiveManager { get; }
    public IFileIoHandler FileIoHandler { get; }
    public IBinInteraction BinInteraction { get; }
    public IFileProperties FileProperties { get; }
    public IShellHandler ShellHandler { get; }
    public IGitIntegration GitIntegration { get; }
    public ILocation GetLocation(string path);
}

public sealed class LocalFileSystem : IFileSystem
{
    IFileInfoProvider IFileSystem.FileInfoProvider => LocalFileInfoProvider;
    public required ILocalFileInfoProvider LocalFileInfoProvider { get; init; }

    public required IIconProvider IconProvider { get; init; }

    IClipboardManager IFileSystem.ClipboardManager => LocalClipboardManager;
    public required ILocalClipboardManager LocalClipboardManager { get; init; }

    public required IArchiveManager ArchiveManager { get; init; }

    public required IFileIoHandler FileIoHandler { get; init; }

    public required IBinInteraction BinInteraction { get; init; }

    public required IFileProperties FileProperties { get; init; }

    public required IShellHandler ShellHandler { get; init; }

    public required IGitIntegration GitIntegration { get; init; }

    public ILocation GetLocation(string path) => new LocalDirLocation(this, path);
}
