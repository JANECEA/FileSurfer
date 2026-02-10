using System;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace FileSurfer.Core.Models.Sftp;

public static class SftpFileSystemFactory
{
    public static ValueResult<SftpFileSystem> TryConnect(SftpConnection connection)
    {
        try
        {
            ConnectionInfo connectionInfo = new(
                connection.HostnameOrIpAddress,
                connection.Port,
                connection.Username,
                new PasswordAuthenticationMethod(connection.Username, connection.Password)
            );
            SftpClient sftpClient = new(connectionInfo);
            bool hostKeyAccepted = false;
            sftpClient.HostKeyReceived += OnSftpClientOnHostKeyReceived;
            sftpClient.Connect();

            if (sftpClient.IsConnected && hostKeyAccepted)
                return ValueResult<SftpFileSystem>.Ok(
                    new SftpFileSystem(connection, sftpClient, GetSshClient(connectionInfo))
                );

            sftpClient.Dispose();
            return ValueResult<SftpFileSystem>.Error("Host key verification failed.");

            void OnSftpClientOnHostKeyReceived(object? _, HostKeyEventArgs e)
            {
                string? algorithm = e.HostKeyName;
                string fingerprintHex = BitConverter
                    .ToString(e.FingerPrint)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();

                FingerPrint? fp = connection.FingerPrints.Find(fp => fp.Algorithm == algorithm);
                if (
                    fp is null
                    || string.Equals(fp.Hash, fingerprintHex, StringComparison.OrdinalIgnoreCase)
                )
                {
                    if (fp is null)
                        connection.FingerPrints.Add(new FingerPrint(algorithm, fingerprintHex));

                    hostKeyAccepted = true;
                    e.CanTrust = true;
                    return;
                }
                e.CanTrust = false;
            }
        }
        catch (Exception ex)
        {
            return ValueResult<SftpFileSystem>.Error($"SFTP connection failed: {ex.Message}");
        }
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
