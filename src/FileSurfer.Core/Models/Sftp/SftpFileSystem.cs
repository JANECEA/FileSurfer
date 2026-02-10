using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.FileOperations;
using FileSurfer.Core.Models.Shell;
using FileSurfer.Core.Models.VersionControl;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Models.Sftp;

public sealed class SftpFileSystem : IFileSystem
{
    private readonly SftpConnection _connection;

    public required IFileInfoProvider FileInfoProvider { get; init; } // TODO
    public required IIconProvider IconProvider { get; init; } // TODO
    public required IClipboardManager ClipboardManager { get; init; } // TODO
    public required IArchiveManager ArchiveManager { get; init; }
    public required IFileIoHandler FileIoHandler { get; init; } // TODO
    public required IBinInteraction BinInteraction { get; init; }
    public required IFileProperties FileProperties { get; init; } // TODO
    public required IShellHandler ShellHandler { get; init; } // TODO
    public required IGitIntegration GitIntegration { get; init; }

    public SftpFileSystem(SftpConnection connection) => _connection = connection;

    public ILocation GetLocation(string path) => throw new System.NotImplementedException("TODO");

    public bool IsSameConnection(SftpConnection connection) =>
        _connection.HostnameOrIpAddress == connection.HostnameOrIpAddress
        && _connection.Port == connection.Port
        && _connection.Username == connection.Username;
}
