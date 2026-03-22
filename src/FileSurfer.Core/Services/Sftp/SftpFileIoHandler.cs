using System;
using System.Collections.Generic;
using System.IO;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Sftp;
using FileSurfer.Core.Services.FileOperations;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace FileSurfer.Core.Services.Sftp;

public sealed class SftpFileIoHandler : IRemoteFileIoHandler
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
            string path = RemoteUnixPathTools.Combine(dirPath, fileName);
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
            _client.CreateDirectory(RemoteUnixPathTools.Combine(dirPath, dirName));
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
        string fileName = RemoteUnixPathTools.GetFileName(filePath);
        string newPath = RemoteUnixPathTools.Combine(destinationDir, fileName);
        return FileOpCommand("cp", filePath, newPath);
    }

    public IResult CopyDirTo(string dirPath, string destinationDir)
    {
        string dirName = RemoteUnixPathTools.GetFileName(dirPath);
        string newPath = RemoteUnixPathTools.Combine(destinationDir, dirName);
        return FileOpCommand("cp -r", dirPath, newPath);
    }

    public IResult DuplicateFile(string filePath, string copyName)
    {
        string parent = RemoteUnixPathTools.GetParentDir(filePath);
        string newPath = RemoteUnixPathTools.Combine(parent, copyName);
        return FileOpCommand("cp", filePath, newPath);
    }

    public IResult DuplicateDir(string dirPath, string copyName)
    {
        string parent = RemoteUnixPathTools.GetParentDir(dirPath);
        string newPath = RemoteUnixPathTools.Combine(parent, copyName);
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

    public IResult WriteFileStream(FileTransferStream fileStream, string dirPath)
    {
        try
        {
            ValueResult<Stream> streamR = fileStream.FileStream;
            if (!streamR.IsOk)
                return streamR;

            _client.DownloadFile(
                RemoteUnixPathTools.Combine(dirPath, fileStream.Name),
                streamR.Value
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult WriteDirStream(DirTransferStream dirStream, string dirPath)
    {
        Queue<(DirTransferStream, string)> queue = new();
        queue.Enqueue((dirStream, dirPath));

        while (queue.Count > 0)
        {
            (DirTransferStream dir, string absParentPath) = queue.Dequeue();
            IResult result = NewDirAt(absParentPath, dir.Name);
            if (!result.IsOk)
                return result;

            foreach (FileTransferStream f in dir.Files)
            {
                result = WriteFileStream(f, absParentPath);
                if (!result.IsOk)
                    return result;
            }

            string newAbsPrentPath = RemoteUnixPathTools.Combine(absParentPath, dir.Name);
            foreach (DirTransferStream d in dir.Directories)
                queue.Enqueue((d, newAbsPrentPath));
        }

        return SimpleResult.Ok();
    }

    private ValueResult<string> Rename(string path, string newName)
    {
        string parent = RemoteUnixPathTools.GetParentDir(path);
        string newPath = RemoteUnixPathTools.Combine(parent, newName);
        return FileOpCommand("mv", path, newPath);
    }

    private ValueResult<string> MoveTo(string path, string destDir)
    {
        string name = RemoteUnixPathTools.GetFileName(path);
        string newPath = RemoteUnixPathTools.Combine(destDir, name);
        return FileOpCommand("mv", path, newPath);
    }

    private ValueResult<string> FileOpCommand(string command, string pathA, string pathB)
    {
        string quotedPathA = SshShellHandler.Quote(pathA);
        string quotedPathB = SshShellHandler.Quote(pathB);
        return _sshShellHandler.ExecuteSshCommand($"{command} {quotedPathA} {quotedPathB}");
    }

    public IResult UploadFile(string localPath, string remotePath)
    {
        try
        {
            using FileStream localStream = new(localPath, FileMode.Open, FileAccess.Read);
            _client.UploadFile(localStream, remotePath, true);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult DownloadFile(string remotePath, string localPath)
    {
        try
        {
            using FileStream localStream = new(localPath, FileMode.OpenOrCreate, FileAccess.Write);
            _client.DownloadFile(remotePath, localStream);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }
}
