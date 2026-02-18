using System;
using System.Threading.Tasks;
using FileSurfer.Core.Services.Dialogs;
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

    private async Task<string?> RequestPassphraseAsync(SftpConnection connection)
    {
        const string title = "Input passphrase";
        string context = $"""
            Please enter the passphrase for:
            key path - "{connection.KeyPath}"
            """;
        return await _dialogService.InputDialog(title, context, true);
    }

    private async Task<ValueResult<AuthenticationMethod>> GetAuthMethod(SftpConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.KeyPath))
        {
            string? password = await RequestPasswordAsync(connection);
            return string.IsNullOrEmpty(password)
                ? ValueResult<AuthenticationMethod>.Error()
                : ValueResult<AuthenticationMethod>.Ok(
                    new PasswordAuthenticationMethod(connection.Username, password)
                );
        }

        string? passphrase = null;
        if (connection.NeedsPassphrase)
        {
            passphrase = await RequestPassphraseAsync(connection);
            if (string.IsNullOrEmpty(passphrase))
                return ValueResult<AuthenticationMethod>.Error();
        }
        return ValueResult<AuthenticationMethod>.Ok(
            new PrivateKeyAuthenticationMethod(
                connection.Username,
                new PrivateKeyFile(connection.KeyPath, passphrase)
            )
        );
    }

    public async Task<ValueResult<SftpFileSystem>> TryConnectAsync(SftpConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.HostnameOrIpAddress))
            return ValueResult<SftpFileSystem>.Error("Missing Hostname or IP address");

        if (connection.Port == 0)
            return ValueResult<SftpFileSystem>.Error("Missing Port");

        if (string.IsNullOrEmpty(connection.Username))
            return ValueResult<SftpFileSystem>.Error("Missing Username");

        try
        {
            ValueResult<AuthenticationMethod> authMethodResult = await GetAuthMethod(connection);
            if (!authMethodResult.IsOk)
                return ValueResult<SftpFileSystem>.Error(authMethodResult);

            ConnectionInfo connectionInfo = new(
                connection.HostnameOrIpAddress,
                connection.Port,
                connection.Username,
                authMethodResult.Value
            );
            SftpClient sftpClient = new(connectionInfo);
            SshClient sshClient = new(connectionInfo);
            HostKeyEventArgs? args = null;
            sftpClient.HostKeyReceived += (_, e) => args = e;
            sftpClient.Connect(); // HostKeyReceived is invoked before Connect returns
            sshClient.Connect();

            if (args is null)
                return ValueResult<SftpFileSystem>.Error("Host key verification failed.");

            if (!await ProcessHostKeyArgs(args, connection))
                return ValueResult<SftpFileSystem>.Error();

            if (sftpClient.IsConnected && sshClient.IsConnected)
                return new SftpFileSystem(
                    connection.HostnameOrIpAddress,
                    sftpClient,
                    sshClient
                ).OkResult();

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

    private async Task<bool> ProcessHostKeyArgs(HostKeyEventArgs e, SftpConnection connection)
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
}
