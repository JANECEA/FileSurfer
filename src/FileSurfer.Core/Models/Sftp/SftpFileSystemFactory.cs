using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Services.Dialogs;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace FileSurfer.Core.Models.Sftp;

/// <summary>
/// Creates and initializes <see cref="SftpFileSystem"/> instances from <see cref="SftpConnection"/> settings.
/// </summary>
public class SftpFileSystemFactory
{
    private enum HostKeyTrustState
    {
        Trusted,
        New,
        Mismatch,
    }

    private sealed record HostKeyDecision(
        FingerPrint NewFingerprint,
        HostKeyTrustState TrustState,
        FingerPrint? ExistingFingerprint
    );

    private static readonly TimeSpan KeepAliveFor = TimeSpan.FromSeconds(15);

    private readonly IDialogService _dialogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SftpFileSystemFactory"/> class.
    /// </summary>
    /// <param name="dialogService">The dialog service used for authentication and host-key prompts.</param>
    public SftpFileSystemFactory(IDialogService dialogService) => _dialogService = dialogService;

    private Task<string?> RequestPasswordAsync(SftpConnection connection)
    {
        const string title = "Input password";
        string context = $"""
            Please enter the password for:
            host - "{connection.HostnameOrIpAddress}"
            username - "{connection.Username}"
            """;
        return _dialogService.InputDialogAsync(title, context, true);
    }

    private Task<string?> RequestPassphraseAsync(SftpConnection connection)
    {
        const string title = "Input passphrase";
        string context = $"""
            Please enter the passphrase for:
            key path - "{connection.KeyPath}"
            """;
        return _dialogService.InputDialogAsync(title, context, true);
    }

    private async Task<ValueResult<AuthenticationMethod>> GetAuthMethodAsync(
        SftpConnection connection
    )
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

    /// <summary>
    /// Attempts to establish an SFTP and SSH connection and create a corresponding file system.
    /// </summary>
    /// <param name="connection">The connection settings to use.</param>
    /// <returns>
    /// A successful result containing the created <see cref="SftpFileSystem"/>; otherwise an error result.
    /// </returns>
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
            return await TryConnectInternalAsync(connection);
        }
        catch (SshOperationTimeoutException)
        {
            return ValueResult<SftpFileSystem>.Error("SFTP connection timed out.");
        }
        catch (OperationCanceledException)
        {
            return ValueResult<SftpFileSystem>.Error();
        }
        catch (Exception ex)
        {
            return ValueResult<SftpFileSystem>.Error(ex.Message);
        }
    }

    private async Task<ValueResult<SftpFileSystem>> TryConnectInternalAsync(
        SftpConnection connection
    )
    {
        ValueResult<AuthenticationMethod> authMethodResult = await GetAuthMethodAsync(connection);
        if (!authMethodResult.IsOk)
            return ValueResult<SftpFileSystem>.Error(authMethodResult);

        ConnectionInfo connectionInfo = new(
            connection.HostnameOrIpAddress,
            connection.Port,
            connection.Username,
            authMethodResult.Value
        );

        for (int attempt = 0; attempt < 2; attempt++)
        {
            (
                SftpClient sftpClient,
                SshClient sshClient,
                HostKeyDecision? pendingDecision,
                Exception? connectException
            ) = await ConnectOnceAsync(connectionInfo, connection);

            if (pendingDecision is not null)
            {
                bool trusted = await ProcessHostKeyDecisionAsync(pendingDecision, connection);
                DisposeClients(sftpClient, sshClient);
                if (!trusted)
                    return ValueResult<SftpFileSystem>.Error();

                continue;
            }

            if (connectException is null && sftpClient.IsConnected && sshClient.IsConnected)
                return new SftpFileSystem(
                    connection.HostnameOrIpAddress,
                    sftpClient,
                    sshClient
                ).OkResult();

            DisposeClients(sftpClient, sshClient);
            if (connectException is not null)
                throw connectException;

            return ValueResult<SftpFileSystem>.Error("Host key verification failed.");
        }

        return ValueResult<SftpFileSystem>.Error("Host key verification failed.");
    }

    private async Task<(
        SftpClient SftpClient,
        SshClient SshClient,
        HostKeyDecision? PendingDecision,
        Exception? ConnectException
    )> ConnectOnceAsync(ConnectionInfo connectionInfo, SftpConnection connection)
    {
        SftpClient sftpClient = new(connectionInfo) { KeepAliveInterval = KeepAliveFor };
        SshClient sshClient = new(connectionInfo) { KeepAliveInterval = KeepAliveFor };
        List<HostKeyDecision> decisions = new();

        sftpClient.HostKeyReceived += (_, e) =>
        {
            HostKeyDecision decision = EvaluateHostKey(e, connection);
            AddHostKeyDecision(decisions, decision);
            e.CanTrust = decision.TrustState is HostKeyTrustState.Trusted;
        };
        sshClient.HostKeyReceived += (_, e) =>
        {
            HostKeyDecision decision = EvaluateHostKey(e, connection);
            AddHostKeyDecision(decisions, decision);
            e.CanTrust = decision.TrustState is HostKeyTrustState.Trusted;
        };

        Exception? connectException = null;
        try
        {
            await _dialogService.BlockingDialogAsync(
                $"Connecting to {connection.HostnameOrIpAddress}",
                async (r, ct) =>
                {
                    IndeterminateReporter rep = new(r);
                    rep.ReportItem("Connecting SFTP client...");
                    await sftpClient.ConnectAsync(ct);
                    rep.ReportItem("Connecting SSH client...");
                    await sshClient.ConnectAsync(ct);
                    return Task.CompletedTask;
                }
            );
        }
        catch (Exception ex)
        {
            connectException = ex;
        }

        return (sftpClient, sshClient, SelectPendingHostKeyDecision(decisions), connectException);
    }

    private Task<bool> ConfirmNewFpAsync(
        SftpConnection connection,
        FingerPrint oldFingerprint,
        FingerPrint newFingerprint
    )
    {
        const string title = "Fingerprint mismatch";
        string algorithm = string.IsNullOrWhiteSpace(oldFingerprint.Algorithm)
            ? "Unknown"
            : oldFingerprint.Algorithm;

        string context = $"""
            The host's SSH fingerprint has changed for {connection.HostnameOrIpAddress}.

            Algorithm: {algorithm}

            Previous fingerprint: {oldFingerprint.Hash}
            New fingerprint:      {newFingerprint.Hash}

            Do you want to trust the new fingerprint and continue connecting?
            """;
        return _dialogService.ConfirmationDialogAsync(title, context);
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

    private static void DisposeClients(SftpClient sftpClient, SshClient sshClient)
    {
        sftpClient.Dispose();
        sshClient.Dispose();
    }

    private static void AddHostKeyDecision(
        List<HostKeyDecision> decisions,
        HostKeyDecision decision
    )
    {
        int index = decisions.FindIndex(d =>
            string.Equals(
                d.NewFingerprint.Algorithm,
                decision.NewFingerprint.Algorithm,
                StringComparison.Ordinal
            )
        );
        if (index == -1)
            decisions.Add(decision);
        else
            decisions[index] = decision;
    }

    private static HostKeyDecision? SelectPendingHostKeyDecision(
        IReadOnlyList<HostKeyDecision> decisions
    )
    {
        HostKeyDecision? mismatch = decisions.FirstOrDefault(d =>
            d.TrustState is HostKeyTrustState.Mismatch
        );
        if (mismatch is not null)
            return mismatch;

        return decisions.FirstOrDefault(d => d.TrustState is HostKeyTrustState.New);
    }

    private static HostKeyDecision EvaluateHostKey(HostKeyEventArgs e, SftpConnection connection)
    {
        string algorithm = e.HostKeyName;
        string fingerprintHex = BitConverter
            .ToString(e.FingerPrint)
            .Replace("-", string.Empty)
            .ToLowerInvariant();

        FingerPrint newFp = new(algorithm, fingerprintHex);
        FingerPrint? oldFp = connection.FingerPrints.Find(fp => fp.Algorithm == algorithm);
        if (oldFp is null)
            return new HostKeyDecision(newFp, HostKeyTrustState.New, null);

        return newFp.IsSame(oldFp)
            ? new HostKeyDecision(newFp, HostKeyTrustState.Trusted, oldFp)
            : new HostKeyDecision(newFp, HostKeyTrustState.Mismatch, oldFp);
    }

    private async Task<bool> ProcessHostKeyDecisionAsync(
        HostKeyDecision decision,
        SftpConnection connection
    )
    {
        switch (decision.TrustState)
        {
            case HostKeyTrustState.Trusted:
                return true;

            case HostKeyTrustState.New:
                AddedNewFpAsync(connection, decision.NewFingerprint);
                connection.FingerPrints.Add(decision.NewFingerprint);
                return true;

            case HostKeyTrustState.Mismatch when decision.ExistingFingerprint is not null:
                bool trust = await ConfirmNewFpAsync(
                    connection,
                    decision.ExistingFingerprint,
                    decision.NewFingerprint
                );
                if (trust)
                    decision.ExistingFingerprint.Hash = decision.NewFingerprint.Hash;
                return trust;

            case HostKeyTrustState.Mismatch:
            default:
                return false;
        }
    }
}
