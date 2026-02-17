using System;
using System.Collections.Generic;
using System.IO;
using FileSurfer.Core.Models.FileOperations;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace FileSurfer.Core.Models.Sftp;

public sealed class SftpFileIoHandler : IFileIoHandler
{
    private readonly SftpClient _client;

    public SftpFileIoHandler(SftpClient client) => _client = client;

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

    private SimpleResult Rename(string path, string newName)
    {
        try
        {
            string parent = path[..path.LastIndexOf('/')];
            _client.RenameFile(path, SftpPathTools.Combine(parent, newName));
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult MoveFileTo(string filePath, string destinationDir) =>
        MoveTo(filePath, destinationDir);

    public IResult MoveDirTo(string dirPath, string destinationDir) =>
        MoveTo(dirPath, destinationDir);

    private SimpleResult MoveTo(string path, string destDir)
    {
        try
        {
            string name = SftpPathTools.GetFileName(path);
            _client.RenameFile(path, SftpPathTools.Combine(destDir, name));
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    private void RoundTripCopy(string sourceFilePath, string destinationPath)
    {
        using MemoryStream ms = new();
        _client.DownloadFile(sourceFilePath, ms);
        ms.Position = 0;
        _client.UploadFile(ms, destinationPath);
    }

    public IResult CopyFileTo(string filePath, string destinationDir)
    {
        try
        {
            string name = SftpPathTools.GetFileName(filePath);
            string dest = SftpPathTools.Combine(destinationDir, name);
            RoundTripCopy(filePath, dest);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult CopyDirTo(string dirPath, string destinationDir)
    {
        try
        {
            string name = SftpPathTools.GetFileName(dirPath);
            string newRoot = SftpPathTools.Combine(destinationDir, name);
            CopyDirectoryIterative(dirPath, newRoot);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult DuplicateFile(string filePath, string copyName)
    {
        try
        {
            string parent = SftpPathTools.GetParentDir(filePath);
            string dest = SftpPathTools.Combine(parent, copyName);
            RoundTripCopy(filePath, dest);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult DuplicateDir(string dirPath, string copyName)
    {
        try
        {
            string parent = SftpPathTools.GetParentDir(dirPath);
            string newRoot = SftpPathTools.Combine(parent, copyName);
            CopyDirectoryIterative(dirPath, newRoot);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    private void CopyDirectoryIterative(string source, string dest)
    {
        Stack<(string Source, string Dest)> stack = new();
        stack.Push((source, dest));

        while (stack.Count > 0)
        {
            (string currentSource, string currentDest) = stack.Pop();
            _client.CreateDirectory(currentDest);
            foreach (ISftpFile entry in _client.ListDirectory(currentSource))
            {
                if (entry.Name is "." or "..")
                    continue;

                string srcPath = entry.FullName;
                string dstPath = SftpPathTools.Combine(currentDest, entry.Name);

                if (entry.IsDirectory)
                    stack.Push((srcPath, dstPath));
                else
                    RoundTripCopy(srcPath, dstPath);
            }
        }
    }

    public IResult DeleteFile(string filePath)
    {
        try
        {
            _client.DeleteFile(filePath);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult DeleteDir(string dirPath)
    {
        try
        {
            DeleteDirIterative(dirPath);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    private void DeleteDirIterative(string path)
    {
        List<string> foundDirs = [path];

        for (int i = 0; i < foundDirs.Count; i++)
            foreach (ISftpFile entry in _client.ListDirectory(foundDirs[i]))
                if (entry.Name is not ("." or ".."))
                {
                    if (entry.IsDirectory)
                        foundDirs.Add(entry.FullName);
                    else
                        _client.DeleteFile(entry.FullName);
                }

        for (int i = foundDirs.Count - 1; i >= 0; i--)
            _client.DeleteDirectory(foundDirs[i]);
    }
}
