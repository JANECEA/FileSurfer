using System;
using System.Diagnostics.CodeAnalysis;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Sftp;
using ReactiveUI;

namespace FileSurfer.Core.ViewModels;

/// <summary>
/// The <see cref="SftpConnectionViewModel"/> is the ViewModel for the <see cref="Views.EditSftpWindow"/>.
/// </summary>
[
    SuppressMessage("ReSharper", "MemberCanBePrivate.Global"),
    SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global"),
]
public sealed class SftpConnectionViewModel : ReactiveObject
{
    private Action<SftpConnectionViewModel>? _addSelf = null;

    public SftpConnection SftpConnection { get; }
    public bool CreateOnSave => _addSelf is not null;

    public SftpFileSystem? FileSystem
    {
        get => _fileSystem;
        set => this.RaiseAndSetIfChanged(ref _fileSystem, value);
    }
    private SftpFileSystem? _fileSystem;

    private string _hostnameOrIpAddress = string.Empty;
    public string HostnameOrIpAddress
    {
        get => _hostnameOrIpAddress;
        set => this.RaiseAndSetIfChanged(ref _hostnameOrIpAddress, value);
    }

    private ushort _port = 22;
    public ushort Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    private string _keyPath = string.Empty;
    public string KeyPath
    {
        get => _keyPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _keyPath, value);
            if (string.IsNullOrWhiteSpace(value))
                NeedsPassphrase = false;
        }
    }

    private bool _needsPassphrase = false;
    public bool NeedsPassphrase
    {
        get => _needsPassphrase;
        set => this.RaiseAndSetIfChanged(ref _needsPassphrase, value);
    }

    private string? _initialDirectory = null;

    public string? InitialDirectory
    {
        get => _initialDirectory;
        set => this.RaiseAndSetIfChanged(ref _initialDirectory, value);
    }

    public SftpConnectionViewModel(SftpConnection sftpConnection)
    {
        SftpConnection = sftpConnection;

        HostnameOrIpAddress = sftpConnection.HostnameOrIpAddress;
        Port = sftpConnection.Port;
        Username = sftpConnection.Username;
        KeyPath = sftpConnection.KeyPath;
        NeedsPassphrase = sftpConnection.NeedsPassphrase;
        InitialDirectory = sftpConnection.InitialDirectory;
    }

    public SftpConnectionViewModel(Action<SftpConnectionViewModel> addSelf)
    {
        SftpConnection = new SftpConnection();
        _addSelf = addSelf;
    }

    public SftpConnectionViewModel() => SftpConnection = new SftpConnection();

    public SftpConnectionViewModel Copy() =>
        new()
        {
            HostnameOrIpAddress = HostnameOrIpAddress,
            Port = Port,
            Username = Username,
            KeyPath = KeyPath,
            NeedsPassphrase = NeedsPassphrase,
            InitialDirectory = InitialDirectory,
        };

    public void Save(SftpConnectionViewModel saveFrom)
    {
        HostnameOrIpAddress = saveFrom.HostnameOrIpAddress.Trim();
        Port = saveFrom.Port;
        Username = saveFrom.Username;
        KeyPath = PathTools.NormalizeLocalPath(saveFrom.KeyPath.Trim());
        InitialDirectory = saveFrom.InitialDirectory?.Trim();

        SftpConnection.HostnameOrIpAddress = HostnameOrIpAddress;
        SftpConnection.Port = Port;
        SftpConnection.Username = Username;
        SftpConnection.KeyPath = KeyPath;
        SftpConnection.NeedsPassphrase = NeedsPassphrase;
        SftpConnection.InitialDirectory = InitialDirectory;

        if (CreateOnSave)
        {
            _addSelf!.Invoke(this);
            _addSelf = null;
        }
    }
}
