using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.FileOperations;
using FileSurfer.Core.Models.Shell;
using FileSurfer.Core.Models.VersionControl;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Models.Sftp;

public sealed class SftpFileSystem : IFileSystem
{
    private readonly SftpConnection _connection;

    public required IFileInfoProvider FileInfoProvider { get; init; }
    public required IIconProvider IconProvider { get; init; } = new BaseIconProvider();
    public required IClipboardManager ClipboardManager { get; init; }
    public IArchiveManager ArchiveManager { get; } = new SftpArchiveManager();
    public required IFileIoHandler FileIoHandler { get; init; }
    public IBinInteraction BinInteraction { get; } = new SftpBinInteraction();
    public IFileProperties FileProperties { get; } = new SftpFileProperties();
    public required IShellHandler ShellHandler { get; init; } // TODO
    public IGitIntegration GitIntegration { get; } = new SftpGitIntegration();

    public SftpFileSystem(SftpConnection connection) => _connection = connection;

    public ILocation GetLocation(string path) => throw new System.NotImplementedException("TODO");

    public bool IsSameConnection(SftpConnection connection) =>
        _connection.HostnameOrIpAddress == connection.HostnameOrIpAddress
        && _connection.Port == connection.Port
        && _connection.Username == connection.Username;
}
