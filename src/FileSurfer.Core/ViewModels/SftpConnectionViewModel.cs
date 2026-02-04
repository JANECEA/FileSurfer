using ReactiveUI;

namespace FileSurfer.Core.ViewModels;

/// <summary>
/// The <see cref="SftpConnectionViewModel"/> is the ViewModel for the <see cref="Views.EditSftpWindow"/>.
/// </summary>
public sealed class SftpConnectionViewModel : ReactiveObject
{
    public bool CreateOnSave { get; } = true;

    private string _hostnameOrIpAddress;
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

    private string _username;
    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    private string _password;
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

    public void Save()
    {
        // TODO
    }
}
