using System;
using System.Linq;
using System.Text;
using FileSurfer.Core.Models.Shell;
using Renci.SshNet;

namespace FileSurfer.Core.Models.Sftp;

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

    private static string Quote(string str)
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

    public ValueResult<string> ExecuteCommand(string programName, params string[] args)
    {
        try
        {
            string argString = string.Join(' ', args.Select(Quote));
            string commandText = $"{Quote(programName)} {argString}".Trim();

            SshCommand cmd = _sshClient.CreateCommand(commandText);
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
