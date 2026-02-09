using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.FileOperations;
using FileSurfer.Core.Models.Shell;
using FileSurfer.Core.Models.VersionControl;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Linux.Models;

public sealed class LinuxFileSystem : IFileSystem
{
    public required IFileInfoProvider FileInfoProvider { get; init; }
    public required IIconProvider IconProvider { get; init; }
    public required IClipboardManager ClipboardManager { get; init; }
    public required IArchiveManager ArchiveManager { get; init; }
    public required IFileIoHandler FileIoHandler { get; init; }
    public required IBinInteraction BinInteraction { get; init; }
    public required IFileProperties FileProperties { get; init; }
    public required IShellHandler ShellHandler { get; init; }
    public required IGitIntegration GitIntegration { get; init; }

    public ILocation GetLocation(string path) => new LocalDirLocation(this, path);
}
