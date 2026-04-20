using System;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Sftp;
using ReactiveUI;

namespace FileSurfer.Core.ViewModels;

/// <summary>
/// The <see cref="SftpConnectionViewModel"/> is the ViewModel for the <see cref="Views.EditSftpWindow"/>.
/// </summary>
public sealed class SftpConnectionViewModel : ReactiveObject
{
    private Action<SftpConnectionViewModel>? _addSelf = null;

    /// <summary>
    /// Gets the underlying SFTP connection model.
    /// </summary>
    public SftpConnection SftpConnection { get; }

    /// <summary>
    /// Gets whether this view model should be added as a new connection when saved.
    /// </summary>
    public bool CreateOnSave => _addSelf is not null;

    /// <summary>
    /// Gets or sets the active SFTP file system instance for this connection.
    /// </summary>
    public SftpFileSystem? FileSystem
    {
        get => _fileSystem;
        set => this.RaiseAndSetIfChanged(ref _fileSystem, value);
    }
    private SftpFileSystem? _fileSystem;

    /// <summary>
    /// Gets or sets the remote host name or IP address.
    /// </summary>
    public string HostnameOrIpAddress
    {
        get => _hostnameOrIpAddress;
        set => this.RaiseAndSetIfChanged(ref _hostnameOrIpAddress, value);
    }
    private string _hostnameOrIpAddress = string.Empty;

    /// <summary>
    /// Gets or sets the remote SSH port.
    /// </summary>
    public ushort Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }
    private ushort _port = 22;

    /// <summary>
    /// Gets or sets the SSH username.
    /// </summary>
    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }
    private string _username = string.Empty;

    /// <summary>
    /// Gets or sets the private key file path.
    /// </summary>
    public string KeyPath
    {
        get => _keyPath;
        set { this.RaiseAndSetIfChanged(ref _keyPath, value); }
    }
    private string _keyPath = string.Empty;

    /// <summary>
    /// Gets or sets whether the private key requires a passphrase.
    /// </summary>
    public bool? NeedsPassphrase
    {
        get => _needsPassphrase;
        set => this.RaiseAndSetIfChanged(ref _needsPassphrase, value);
    }
    private bool? _needsPassphrase = false;

    /// <summary>
    /// Gets or sets the initial remote directory after connecting.
    /// </summary>
    public string? InitialDirectory
    {
        get => _initialDirectory;
        set => this.RaiseAndSetIfChanged(ref _initialDirectory, value);
    }
    private string? _initialDirectory = null;

    /// <summary>
    /// Creates a connection view model from an existing SFTP connection model.
    /// </summary>
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

    /// <summary>
    /// Creates a new connection view model that adds itself to a target collection on save.
    /// </summary>
    public SftpConnectionViewModel(Action<SftpConnectionViewModel> addSelf)
    {
        SftpConnection = new SftpConnection();
        _addSelf = addSelf;
    }

    /// <summary>
    /// Creates a new empty connection view model.
    /// </summary>
    public SftpConnectionViewModel() => SftpConnection = new SftpConnection();

    /// <summary>
    /// Creates a detached copy of the editable connection fields.
    /// </summary>
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

    /// <summary>
    /// Applies values from another view model, updates the model, and finalizes creation when needed.
    /// </summary>
    public void Save(SftpConnectionViewModel saveFrom)
    {
        HostnameOrIpAddress = saveFrom.HostnameOrIpAddress.Trim();
        Port = saveFrom.Port;
        Username = saveFrom.Username;
        KeyPath = LocalPathTools.NormalizePath(saveFrom.KeyPath.Trim());
        NeedsPassphrase = saveFrom.NeedsPassphrase;
        if (string.IsNullOrWhiteSpace(KeyPath))
            NeedsPassphrase = false;

        InitialDirectory = saveFrom.InitialDirectory?.Trim();

        SftpConnection.HostnameOrIpAddress = HostnameOrIpAddress;
        SftpConnection.Port = Port;
        SftpConnection.Username = Username;
        SftpConnection.KeyPath = KeyPath;
        SftpConnection.NeedsPassphrase = NeedsPassphrase ?? false;
        SftpConnection.InitialDirectory = InitialDirectory;

        if (CreateOnSave)
        {
            _addSelf!.Invoke(this);
            _addSelf = null;
        }
    }
}
