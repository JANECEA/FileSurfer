using System;
using System.Linq;
using System.Text;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;
using Renci.SshNet;

namespace FileSurfer.Core.Services.Sftp;

public sealed class SftpShellHandler : IShellHandler
{
    private const char QuoteChar = '\'';
    private readonly SshClient _sshClient;
    private readonly SftpClient _sftpClient;

    public SftpShellHandler(SshClient sshClient, SftpClient sftpClient)
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
        CreateLinkInternal(PathTools.NormalizePath(filePath), ".link");

    public IResult CreateDirectoryLink(string dirPath) =>
        CreateLinkInternal(PathTools.NormalizePath(dirPath), "-link");

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

    public ValueResult<string> ExecuteSshCommand(string command)
    {
        try
        {
            SshCommand cmd = _sshClient.CreateCommand(command);
            string result = cmd.Execute();

            return cmd.ExitStatus == 0
                ? ValueResult<string>.Ok(result)
                : ValueResult<string>.Error(cmd.Error);
        }
        catch (Exception ex)
        {
            return ValueResult<string>.Error(ex.Message);
        }
    }
}
