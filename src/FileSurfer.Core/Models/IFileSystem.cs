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
