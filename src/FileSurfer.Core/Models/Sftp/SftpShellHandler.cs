using System;
using FileSurfer.Core.Models.Shell;
using Renci.SshNet;

namespace FileSurfer.Core.Models.Sftp;

public sealed class SftpShellHandler : IShellHandler
{
    private readonly SshClient _ssh;

    public SftpShellHandler(SshClient ssh) => _ssh = ssh;

    public IResult CreateFileLink(string filePath) => throw new NotImplementedException();

    public IResult CreateDirectoryLink(string dirPath) => throw new NotImplementedException();

    public IResult OpenCmdAt(string dirPath) => throw new NotImplementedException();

    public ValueResult<string> ExecuteCommand(string programName, params string[] args) =>
        throw new NotImplementedException();
}
