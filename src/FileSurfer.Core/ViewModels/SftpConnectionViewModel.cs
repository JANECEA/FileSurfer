using System;
using System.Diagnostics.CodeAnalysis;
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

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
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
        Password = sftpConnection.Password;
        InitialDirectory = sftpConnection.InitialDirectory;
    }

    public SftpConnectionViewModel(Action<SftpConnectionViewModel>? addSelf = null)
    {
        SftpConnection = new SftpConnection();
        _addSelf = addSelf;
    }

    public SftpConnectionViewModel Copy() =>
        new()
        {
            HostnameOrIpAddress = HostnameOrIpAddress,
            Port = Port,
            Username = Username,
            Password = Password,
            InitialDirectory = InitialDirectory,
        };

    public void Save(SftpConnectionViewModel saveFrom)
    {
        HostnameOrIpAddress = saveFrom.HostnameOrIpAddress.Trim();
        Port = saveFrom.Port;
        Username = saveFrom.Username;
        Password = saveFrom.Password;
        InitialDirectory = saveFrom.InitialDirectory?.Trim();

        SftpConnection.HostnameOrIpAddress = HostnameOrIpAddress;
        SftpConnection.Port = Port;
        SftpConnection.Username = Username;
        SftpConnection.Password = Password;
        SftpConnection.InitialDirectory = InitialDirectory;

        if (CreateOnSave)
        {
            _addSelf!.Invoke(this);
            _addSelf = null;
        }
    }
}
