using System;
using System.Diagnostics.CodeAnalysis;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.Sftp;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.Services.VersionControl;
using Renci.SshNet;

namespace FileSurfer.Core.Models.Sftp;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public sealed class SftpFileSystem : IFileSystem, IDisposable
{
    private const string EnvironmentNotSupported = "The SFTP environment is not supported.";
    private readonly SftpClient _sftpClient;
    private readonly SshClient _sshClient;
    private readonly string _label;
    private bool _disposed = false;

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
    public SshShellHandler ShellHandler { get; }
    public StubGitIntegration GitIntegration { get; } = new(EnvironmentNotSupported);

    public SftpFileSystem(string label, SftpClient sftpClient, SshClient sshClient)
    {
        _sftpClient = sftpClient;
        _sshClient = sshClient;
        _label = label;

        ShellHandler = new SshShellHandler(_sshClient, _sftpClient);
        FileInfoProvider = new SftpFileInfoProvider(_sftpClient, ShellHandler);
        FileIoHandler = new SftpFileIoHandler(_sftpClient, ShellHandler);
        ClipboardManager = new BasicClipboardManager(FileInfoProvider, FileIoHandler);
    }

    public bool IsReady() => !_disposed;

    public string GetLabel() => _label;

    public void Dispose()
    {
        _sftpClient.Dispose();
        _sshClient.Dispose();
        IconProvider.Dispose();
        GitIntegration.Dispose();
        _disposed = true;
    }
}
