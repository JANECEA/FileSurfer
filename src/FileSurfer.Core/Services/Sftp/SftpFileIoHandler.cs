using System;
using System.IO;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Sftp;
using FileSurfer.Core.Services.FileOperations;
using Renci.SshNet;

namespace FileSurfer.Core.Services.Sftp;

public sealed class SftpFileIoHandler : IFileIoHandler
{
    private readonly SshShellHandler _sshShellHandler;
    private readonly SftpClient _client;

    public SftpFileIoHandler(SftpClient client, SshShellHandler sshShellHandler)
    {
        _client = client;
        _sshShellHandler = sshShellHandler;
    }

    public IResult NewFileAt(string dirPath, string fileName)
    {
        try
        {
            string path = SftpPathTools.Combine(dirPath, fileName);
            using MemoryStream stream = new();
            _client.UploadFile(stream, path);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult NewDirAt(string dirPath, string dirName)
    {
        try
        {
            _client.CreateDirectory(SftpPathTools.Combine(dirPath, dirName));
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult RenameFileAt(string filePath, string newName) => Rename(filePath, newName);

    public IResult RenameDirAt(string dirPath, string newName) => Rename(dirPath, newName);

    public IResult MoveFileTo(string filePath, string destinationDir) =>
        MoveTo(filePath, destinationDir);

    public IResult MoveDirTo(string dirPath, string destinationDir) =>
        MoveTo(dirPath, destinationDir);

    public IResult CopyFileTo(string filePath, string destinationDir)
    {
        string fileName = SftpPathTools.GetFileName(filePath);
        string newPath = SftpPathTools.Combine(destinationDir, fileName);
        return FileOpCommand("cp", filePath, newPath);
    }

    public IResult CopyDirTo(string dirPath, string destinationDir)
    {
        string dirName = SftpPathTools.GetFileName(dirPath);
        string newPath = SftpPathTools.Combine(destinationDir, dirName);
        return FileOpCommand("cp -r", dirPath, newPath);
    }

    public IResult DuplicateFile(string filePath, string copyName)
    {
        string parent = SftpPathTools.GetParentDir(filePath);
        string newPath = SftpPathTools.Combine(parent, copyName);
        return FileOpCommand("cp", filePath, newPath);
    }

    public IResult DuplicateDir(string dirPath, string copyName)
    {
        string parent = SftpPathTools.GetParentDir(dirPath);
        string newPath = SftpPathTools.Combine(parent, copyName);
        return FileOpCommand("cp -r", dirPath, newPath);
    }

    public IResult DeleteFile(string filePath)
    {
        string quotedPath = SshShellHandler.Quote(filePath);
        return _sshShellHandler.ExecuteSshCommand($"rm -f {quotedPath}");
    }

    public IResult DeleteDir(string dirPath)
    {
        string quotedPath = SshShellHandler.Quote(dirPath);
        return _sshShellHandler.ExecuteSshCommand($"rm -rf {quotedPath}");
    }

    private ValueResult<string> Rename(string path, string newName)
    {
        string parent = SftpPathTools.GetParentDir(path);
        string newPath = SftpPathTools.Combine(parent, newName);
        return FileOpCommand("mv", path, newPath);
    }

    private ValueResult<string> MoveTo(string path, string destDir)
    {
        string name = SftpPathTools.GetFileName(path);
        string newPath = SftpPathTools.Combine(destDir, name);
        return FileOpCommand("mv", path, newPath);
    }

    private ValueResult<string> FileOpCommand(string command, string pathA, string pathB)
    {
        string quotedPathA = SshShellHandler.Quote(pathA);
        string quotedPathB = SshShellHandler.Quote(pathB);
        return _sshShellHandler.ExecuteSshCommand($"{command} {quotedPathA} {quotedPathB}");
    }
}
