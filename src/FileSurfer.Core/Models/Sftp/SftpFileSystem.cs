using System;
using System.Diagnostics.CodeAnalysis;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.FileOperations;
using FileSurfer.Core.Models.Shell;
using FileSurfer.Core.Models.VersionControl;
using Renci.SshNet;

namespace FileSurfer.Core.Models.Sftp;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public sealed class SftpFileSystem : IFileSystem, IDisposable
{
    private const string EnvironmentNotSupported = "The SFTP environment is not supported.";
    private readonly SftpClient _sftpClient;
    private readonly SshClient? _sshClient;

    IFileInfoProvider IFileSystem.FileInfoProvider => FileInfoProvider;
    IIconProvider IFileSystem.IconProvider => IconProvider;
    IClipboardManager IFileSystem.ClipboardManager => ClipboardManager;
    IArchiveManager IFileSystem.ArchiveManager => ArchiveManager;
    IFileIoHandler IFileSystem.FileIoHandler => FileIoHandler;
    IBinInteraction IFileSystem.BinInteraction => BinInteraction;
    IFileProperties IFileSystem.FileProperties => FileProperties;
    IShellHandler IFileSystem.ShellHandler => ShellHandler;
    IGitIntegration IFileSystem.GitIntegration => GitIntegration;

    public SftpFileInfoProvider FileInfoProvider { get; }
    public BaseIconProvider IconProvider { get; } = new();
    public BasicClipboardManager ClipboardManager { get; }
    public StubArchiveManager ArchiveManager { get; } = new(EnvironmentNotSupported);
    public SftpFileIoHandler FileIoHandler { get; }
    public StubBinInteraction BinInteraction { get; } = new(EnvironmentNotSupported);
    public StubFileProperties FileProperties { get; } = new(EnvironmentNotSupported);
    public IShellHandler ShellHandler { get; }
    public StubGitIntegration GitIntegration { get; } = new(EnvironmentNotSupported);

    public SftpFileSystem(SftpClient sftpClient, SshClient? sshClient)
    {
        _sftpClient = sftpClient;
        _sshClient = sshClient;

        FileInfoProvider = new SftpFileInfoProvider(_sftpClient);
        FileIoHandler = new SftpFileIoHandler(_sftpClient);
        ClipboardManager = new BasicClipboardManager(FileInfoProvider, FileIoHandler);
        ShellHandler = _sshClient is null
            ? new StubShellHandler("The server refused ssh connection")
            : new SftpShellHandler(_sshClient);
    }

    public void Dispose()
    {
        _sftpClient.Dispose();
        _sshClient?.Dispose();
        IconProvider.Dispose();
        GitIntegration.Dispose();
    }
}
