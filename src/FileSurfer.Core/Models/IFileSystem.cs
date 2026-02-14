using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.FileOperations;
using FileSurfer.Core.Models.Shell;
using FileSurfer.Core.Models.VersionControl;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Models;

/// <summary>
/// Represents a generic file system and provides its functionality
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

    public Location GetLocation(string path) => new(this, path);

    public bool IsReady();

    public string GetLabel();
}

/// <summary>
/// Represents a local file system with more specialized versions of <see cref="IFileSystem"/>'s models
/// </summary>
public sealed class LocalFileSystem : IFileSystem
{
    private const string Label = "local";

    IFileInfoProvider IFileSystem.FileInfoProvider => LocalFileInfoProvider;
    IClipboardManager IFileSystem.ClipboardManager => LocalClipboardManager;
    IShellHandler IFileSystem.ShellHandler => LocalShellHandler;

    public required ILocalFileInfoProvider LocalFileInfoProvider { get; init; }
    public required IIconProvider IconProvider { get; init; }
    public required ILocalClipboardManager LocalClipboardManager { get; init; }
    public required IArchiveManager ArchiveManager { get; init; }
    public required IFileIoHandler FileIoHandler { get; init; }
    public required IBinInteraction BinInteraction { get; init; }
    public required IFileProperties FileProperties { get; init; }
    public required ILocalShellHandler LocalShellHandler { get; init; }
    public required IGitIntegration GitIntegration { get; init; }

    public bool IsReady() => true;

    public string GetLabel() => Label;
}
