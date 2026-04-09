using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.Sftp;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace FileSurfer.Core.Models.Sftp;

public sealed class SftpFileInfoProvider : IFileInfoProvider
{
    private readonly SshShellHandler _sshShellHandler;
    private readonly SftpClient _client;

    public IPathTools PathTools => RemoteUnixPathTools.Instance;

    public SftpFileInfoProvider(SftpClient client, SshShellHandler sshShellHandler)
    {
        _client = client;
        _sshShellHandler = sshShellHandler;
    }

    public bool IsLinkedToDirectory(string linkPath, out string directory)
    {
        try // Fast path
        {
            if (_client.Get(linkPath).IsSymbolicLink)
            {
                _ = _client.ListDirectory(linkPath);
                directory = linkPath;
                return true;
            }
        }
        catch
        {
            // Might be false negative, try ssh
        }

        string path = SshShellHandler.Quote(linkPath);
        ValueResult<string> result = _sshShellHandler.ExecuteSshCommand(
            $"test -L {path} && test -d {path} && readlink -f {path}"
        );

        directory = result.IsOk ? RemoteUnixPathTools.NormalizePath(result.Value.Trim()) : null!;
        return result.IsOk && !string.IsNullOrWhiteSpace(directory);
    }

    private IEnumerable<ISftpFile> ListDir(string path) =>
        _client.ListDirectory(path).Where(e => e.Name is not ("." or ".."));

    public ValueResult<List<DirectoryEntryInfo>> GetPathDirs(
        string path,
        bool includeHidden,
        bool includeOs
    )
    {
        try
        {
            IEnumerable<ISftpFile> dirs = ListDir(path).Where(e => e.IsDirectory);
            if (!includeHidden)
                dirs = dirs.Where(e => !IsHidden(e.FullName, true));

            return dirs.Select(MakeDirInfo).ToList().OkResult();
        }
        catch (Exception ex)
        {
            return ValueResult<List<DirectoryEntryInfo>>.Error(ex.Message);
        }
    }

    private static DirectoryEntryInfo MakeDirInfo(ISftpFile dir) =>
        new(dir.FullName, dir.Name, dir.LastWriteTime, dir.LastWriteTimeUtc);

    public ValueResult<List<FileEntryInfo>> GetPathFiles(
        string path,
        bool includeHidden,
        bool includeOs
    )
    {
        try
        {
            IEnumerable<ISftpFile> files = ListDir(path)
                .Where(e => e.IsRegularFile || e.IsSymbolicLink);
            if (!includeHidden)
                files = files.Where(e => !IsHidden(e.FullName, false));

            return files.Select(MakeFileInfo).ToList().OkResult();
        }
        catch (Exception ex)
        {
            return ValueResult<List<FileEntryInfo>>.Error(ex.Message);
        }
    }

    public ValueResult<Stream> GetFileStream(string path)
    {
        try
        {
            return _client.OpenRead(path).OkResult<Stream>();
        }
        catch (Exception ex)
        {
            return ValueResult<Stream>.Error(ex.Message);
        }
    }

    private static FileEntryInfo MakeFileInfo(ISftpFile file) =>
        new(
            file.FullName,
            file.Name,
            RemoteUnixPathTools.GetExtension(file.FullName),
            file.Length,
            file.LastWriteTime,
            file.LastWriteTimeUtc
        );

    public async Task<DateTime?> GetFileLastWriteUtcAsync(string filePath)
    {
        try
        {
            ISftpFile file = await _client.GetAsync(filePath, CancellationToken.None);
            return file.LastWriteTimeUtc;
        }
        catch
        {
            return null;
        }
    }

    public async Task<DateTime?> GetDirLastWriteUtcAsync(string dirPath)
    {
        try
        {
            ISftpFile dir = await _client.GetAsync(dirPath, CancellationToken.None);
            return dir.LastWriteTimeUtc;
        }
        catch
        {
            return null;
        }
    }

    public bool IsHidden(string path, bool isDirectory)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        int i = path.Length - 1;
        while (i >= 0 && path[i] == '/')
            i--;

        for (; i >= 0; i--)
            if (path[i] == '/')
                break;

        return path[i + 1] == '.';
    }

    public string GetRoot() => RemoteUnixPathTools.RootDir;

    public ExistsInfo Exists(string path)
    {
        try
        {
            return ExistsInternal(_client.Get(path));
        }
        catch
        {
            return ExistsInfo.DoesNotExist();
        }
    }

    public async Task<ExistsInfo> ExistsAsync(string path)
    {
        try
        {
            return ExistsInternal(await _client.GetAsync(path, CancellationToken.None));
        }
        catch
        {
            return ExistsInfo.DoesNotExist();
        }
    }

    private static ExistsInfo ExistsInternal(ISftpFile entry)
    {
        if (entry.IsRegularFile || entry.IsSymbolicLink)
            return ExistsInfo.ExistsAsFile();

        return entry.IsDirectory ? ExistsInfo.ExistsAsDirectory() : ExistsInfo.DoesNotExist();
    }
}
