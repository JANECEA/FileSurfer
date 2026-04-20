using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;
using Renci.SshNet;

namespace FileSurfer.Core.Services.Sftp;

/// <summary>
/// Executes shell operations against a remote host over SSH and SFTP.
/// </summary>
public sealed class SshShellHandler : IShellHandler
{
    private const char QuoteChar = '\'';
    private readonly SshClient _sshClient;
    private readonly SftpClient _sftpClient;

    /// <summary>
    /// Initializes a shell handler that uses the provided SSH and SFTP clients.
    /// </summary>
    /// <param name="sshClient">
    /// Connected SSH client used to execute remote shell commands.
    /// </param>
    /// <param name="sftpClient">
    /// Connected SFTP client used for link-related file-system operations.
    /// </param>
    public SshShellHandler(SshClient sshClient, SftpClient sftpClient)
    {
        _sshClient = sshClient;
        _sftpClient = sftpClient;
    }

    /// <summary>
    /// Wraps a string in single quotes and escapes embedded single quotes for safe shell usage.
    /// </summary>
    /// <param name="str">
    /// The raw argument text to quote for remote shell command composition.
    /// </param>
    /// <returns>
    /// A shell-quoted argument string that preserves the original value.
    /// </returns>
    public static string Quote(string str)
    {
        StringBuilder sb = new(str.Length + 2);
        sb.Append(QuoteChar);

        foreach (char c in str)
            if (c == QuoteChar)
                sb.Append("'\\''");
            else
                sb.Append(c);

        sb.Append(QuoteChar);
        return sb.ToString();
    }

    public IResult CreateFileLink(string filePath) =>
        CreateLinkInternal(RemoteUnixPathTools.NormalizePath(filePath), ".link");

    public IResult CreateDirectoryLink(string dirPath) =>
        CreateLinkInternal(RemoteUnixPathTools.NormalizePath(dirPath), "-link");

    private SimpleResult CreateLinkInternal(string path, string suffix)
    {
        try
        {
            _sftpClient.SymbolicLink(path, path + suffix);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    ValueResult<string> IShellHandler.ExecuteCommand(string programName, string[] args) =>
        ExecuteSshCommand($"{Quote(programName)} {string.Join(' ', args.Select(Quote))}");

    public Task<ValueResult<string>> ExecuteCommandAsync(
        string programName,
        params string[] args
    ) => ExecuteSshCommandAsync($"{Quote(programName)} {string.Join(' ', args.Select(Quote))}");

    /// <summary>
    /// Executes a raw command string on the remote SSH session and captures command output.
    /// </summary>
    /// <param name="command">
    /// The fully composed command line to execute remotely.
    /// </param>
    /// <returns>
    /// A command result containing output text on success or error details on failure.
    /// </returns>
    public ValueResult<string> ExecuteSshCommand(string command)
    {
        try
        {
            SshCommand cmd = _sshClient.CreateCommand(command);
            string stdOut = cmd.Execute();
            string stdErr = cmd.Error;
            if (string.IsNullOrWhiteSpace(stdOut))
                stdOut = stdErr;

            if (string.IsNullOrWhiteSpace(stdErr))
                stdErr = stdOut;

            return cmd.ExitStatus == 0
                ? ValueResult<string>.Ok(stdOut)
                : ValueResult<string>.Error(stdErr);
        }
        catch (Exception ex)
        {
            return ValueResult<string>.Error(ex.Message);
        }
    }

    private async Task<ValueResult<string>> ExecuteSshCommandAsync(string command)
    {
        try
        {
            SshCommand cmd = _sshClient.CreateCommand(command);
            await cmd.ExecuteAsync().ConfigureAwait(false);

            string stdOut = cmd.Result;
            string stdErr = cmd.Error;
            if (string.IsNullOrWhiteSpace(stdOut))
                stdOut = stdErr;

            if (string.IsNullOrWhiteSpace(stdErr))
                stdErr = stdOut;

            return cmd.ExitStatus == 0
                ? ValueResult<string>.Ok(stdOut)
                : ValueResult<string>.Error(stdErr);
        }
        catch (Exception ex)
        {
            return ValueResult<string>.Error(ex.Message);
        }
    }
}
