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
    private readonly SftpConnection _sftpConnection;
    private readonly Action<SftpConnectionViewModel>? _addSelf = null;

    public bool CreateOnSave => _addSelf is not null;

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
        _sftpConnection = sftpConnection;

        HostnameOrIpAddress = sftpConnection.hostnameOrIpAddress;
        Port = sftpConnection.port;
        Username = sftpConnection.username;
        Password = sftpConnection.password;
        InitialDirectory = sftpConnection.initialDirectory;
    }

    public SftpConnectionViewModel(Action<SftpConnectionViewModel> addSelf)
    {
        _sftpConnection = new SftpConnection();
        _addSelf = addSelf;
    }

    public void Save()
    {
        _sftpConnection.hostnameOrIpAddress = HostnameOrIpAddress;
        _sftpConnection.port = Port;
        _sftpConnection.username = Username;
        _sftpConnection.password = Password;
        _sftpConnection.initialDirectory = InitialDirectory;

        if (CreateOnSave)
            _addSelf!.Invoke(this);
    }
}
