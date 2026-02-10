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

    public SftpShellHandler(SshClient sshClient) => _sshClient = sshClient;

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

    public IResult CreateFileLink(string filePath)
    {
        try
        {
            string linkPath = filePath + ".lnk";
            Execute($"ln -s {Quote(filePath)} {Quote(linkPath)}");
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult CreateDirectoryLink(string dirPath)
    {
        try
        {
            string linkPath = dirPath.TrimEnd(SftpPathTools.DirSeparator) + "-link";
            Execute($"ln -s {Quote(dirPath)} {Quote(linkPath)}");
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

    private void Execute(string command)
    {
        SshCommand cmd = _sshClient.CreateCommand(command);
        cmd.Execute();

        if (cmd.ExitStatus != 0)
            throw new Exception(cmd.Error);
    }
}
