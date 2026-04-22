using System.Diagnostics.CodeAnalysis;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.Sftp;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.Services.VersionControl;
using Renci.SshNet;

namespace FileSurfer.Core.Models.Sftp;

/// <summary>
/// Represents an SFTP-backed implementation of <see cref="IFileSystem"/>.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public sealed class SftpFileSystem : IFileSystem
{
    private readonly SftpClient _sftpClient;
    private readonly SshClient _sshClient;
    private readonly string _label;

    private readonly SftpFileInfoProvider _fileInfoProvider;
    private readonly BaseIconProvider _iconProvider = new();
    private readonly StubArchiveManager _archiveManager = new(
        "Archivation is not supported on SFTP file systems."
    );
    private readonly SftpFileIoHandler _fileIoHandler;
    private readonly StubBinInteraction _binInteraction = new(
        "Trash is not supported on SFTP file systems."
    );
    private readonly SftpFileProperties _fileProperties;
    private readonly SshShellHandler _shellHandler;
    private readonly StubGitIntegration _gitIntegration = new(
        "Git is not supported on SFTP file systems."
    );

    private bool _disposed = false;

    public IFileInfoProvider FileInfoProvider => _fileInfoProvider;
    public IIconProvider IconProvider => _iconProvider;
    public IArchiveManager ArchiveManager => _archiveManager;
    public IFileIoHandler FileIoHandler => _fileIoHandler;
    public IBinInteraction BinInteraction => _binInteraction;
    public IFileProperties FileProperties => _fileProperties;
    public IShellHandler ShellHandler => _shellHandler;
    public IGitIntegration GitIntegration => _gitIntegration;

    /// <summary>
    /// Initializes a new instance of the <see cref="SftpFileSystem"/> class.
    /// </summary>
    /// <param name="label">The display label for this file system.</param>
    /// <param name="sftpClient">The connected SFTP client.</param>
    /// <param name="sshClient">The connected SSH client.</param>
    public SftpFileSystem(string label, SftpClient sftpClient, SshClient sshClient)
    {
        _sftpClient = sftpClient;
        _sshClient = sshClient;
        _label = label;

        _shellHandler = new SshShellHandler(_sshClient, _sftpClient);
        _fileInfoProvider = new SftpFileInfoProvider(_sftpClient, _shellHandler);
        _fileIoHandler = new SftpFileIoHandler(_sftpClient, _shellHandler);
        _fileProperties = new SftpFileProperties(sftpClient, _shellHandler);
    }

    public bool IsReady() => !_disposed && _sftpClient.IsConnected && _sshClient.IsConnected;

    public bool IsLocal() => false;

    public string GetLabel() => _label;

    public void Dispose()
    {
        _sftpClient.Dispose();
        _sshClient.Dispose();
        _iconProvider.Dispose();
        _gitIntegration.Dispose();
        _disposed = true;
    }
}
