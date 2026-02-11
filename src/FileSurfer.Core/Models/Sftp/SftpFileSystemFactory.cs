using System;
using System.Threading.Tasks;
using FileSurfer.Core.ViewModels;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace FileSurfer.Core.Models.Sftp;

public class SftpFileSystemFactory
{
    private readonly IDialogService _dialogService;

    public SftpFileSystemFactory(IDialogService dialogService) => _dialogService = dialogService;

    private async Task<string?> RequestPasswordAsync(SftpConnection connection)
    {
        const string title = "Input password";
        string context = $"""
            Please enter the password for:
                    host - "{connection.HostnameOrIpAddress}"
                username - "{connection.Username}"
            """;
        return await _dialogService.InputDialog(title, context, true);
    }

    public async Task<ValueResult<SftpFileSystem>> TryConnectAsync(SftpConnection connection)
    {
        string? password = !string.IsNullOrEmpty(connection.Password)
            ? connection.Password
            : await RequestPasswordAsync(connection);

        if (string.IsNullOrEmpty(password))
            return ValueResult<SftpFileSystem>.Error();
        try
        {
            ConnectionInfo connectionInfo = new(
                connection.HostnameOrIpAddress,
                connection.Port,
                connection.Username,
                new PasswordAuthenticationMethod(connection.Username, password)
            );
            SftpClient sftpClient = new(connectionInfo);
            HostKeyEventArgs? args = null;
            sftpClient.HostKeyReceived += (_, e) => args = e;
            sftpClient.Connect(); // HostKeyReceived is invoked before Connect returns

            if (args is null)
                return ValueResult<SftpFileSystem>.Error("Host key verification failed.");

            if (!await HostKeyReceivedAsync(args, connection))
                return ValueResult<SftpFileSystem>.Error();

            if (sftpClient.IsConnected)
                return ValueResult<SftpFileSystem>.Ok(
                    new SftpFileSystem(sftpClient, GetSshClient(connectionInfo))
                );

            sftpClient.Dispose();
            return ValueResult<SftpFileSystem>.Error("Host key verification failed.");
        }
        catch (SshOperationTimeoutException)
        {
            return ValueResult<SftpFileSystem>.Error("SFTP connection timed out");
        }
        catch (Exception ex)
        {
            return ValueResult<SftpFileSystem>.Error(ex.Message);
        }
    }

    private async Task<bool> ConfirmNewFpAsync(
        SftpConnection connection,
        FingerPrint oldFingerprint,
        FingerPrint newFingerprint
    )
    {
        const string title = "Fingerprint mismatch";
        string context = $"""
            The host's SSH fingerprint has changed for {connection.HostnameOrIpAddress}.

            Algorithm: {(
                string.IsNullOrEmpty(oldFingerprint.Algorithm)
                    ? "Unknown"
                    : oldFingerprint.Algorithm
            )}

            Previous fingerprint: {oldFingerprint.Hash}
            New fingerprint:      {newFingerprint.Hash}

            Do you want to trust the new fingerprint and continue connecting?
            """;
        return await _dialogService.ConfirmationDialog(title, context);
    }

    private void AddedNewFpAsync(SftpConnection connection, FingerPrint newFingerprint)
    {
        const string title = "New fingerprint";
        string context = $"""
              Added new fingerprint: {newFingerprint.Hash}
              for SSH host: "{connection.HostnameOrIpAddress}".
            """;
        _dialogService.InfoDialog(title, context);
    }

    private async Task<bool> HostKeyReceivedAsync(HostKeyEventArgs e, SftpConnection connection)
    {
        string algorithm = e.HostKeyName;
        string fingerprintHex = BitConverter
            .ToString(e.FingerPrint)
            .Replace("-", string.Empty)
            .ToLowerInvariant();

        FingerPrint? oldFp = connection.FingerPrints.Find(fp => fp.Algorithm == algorithm);
        FingerPrint newFp = new(algorithm, fingerprintHex);

        if (oldFp is null)
        {
            AddedNewFpAsync(connection, newFp);
            connection.FingerPrints.Add(newFp);
            e.CanTrust = true;
        }
        else if (newFp.IsSame(oldFp))
            e.CanTrust = true;
        else
        {
            e.CanTrust = await ConfirmNewFpAsync(connection, oldFp, newFp);
            if (e.CanTrust)
                oldFp.Hash = newFp.Hash;
        }

        return e.CanTrust;
    }

    private static SshClient? GetSshClient(ConnectionInfo connectionInfo)
    {
        SshClient? sshClient = null;
        try
        {
            sshClient = new SshClient(connectionInfo);
            sshClient.Connect();

            if (!sshClient.IsConnected)
            {
                sshClient.Dispose();
                sshClient = null;
            }
        }
        catch
        {
            sshClient?.Dispose();
            sshClient = null;
        }
        return sshClient;
    }
}
