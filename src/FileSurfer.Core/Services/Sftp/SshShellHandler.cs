using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;
using Renci.SshNet;

namespace FileSurfer.Core.Services.Sftp;

public sealed class SshShellHandler : IShellHandler
{
    private const char QuoteChar = '\'';
    private readonly SshClient _sshClient;
    private readonly SftpClient _sftpClient;

    public SshShellHandler(SshClient sshClient, SftpClient sftpClient)
    {
        _sshClient = sshClient;
        _sftpClient = sftpClient;
    }

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
