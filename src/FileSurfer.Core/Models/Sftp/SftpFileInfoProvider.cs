using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
        string path = SshShellHandler.Quote(linkPath);
        ValueResult<string> result = _sshShellHandler.ExecuteSshCommand(
            $"test -L {path} && test -d {path} && readlink -f {path}"
        );

        directory = result.IsOk ? RemoteUnixPathTools.NormalizePath(result.Value.Trim()) : null!;
        return result.IsOk && !string.IsNullOrWhiteSpace(directory);
    }

    private IEnumerable<ISftpFile> ListDir(string path, bool includeHidden)
    {
        IEnumerable<ISftpFile> entries = _client
            .ListDirectory(path)
            .Where(e => e.Name is not ("." or ".."));

        return includeHidden ? entries : entries.Where(e => !IsHidden(e.FullName, e.IsDirectory));
    }

    private async IAsyncEnumerable<ISftpFile> ListDirAsync(
        string path,
        bool includeHidden,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        await foreach (ISftpFile e in _client.ListDirectoryAsync(path, ct))
            if (
                e.Name is not ("." or "..")
                && (includeHidden || !IsHidden(e.FullName, e.IsDirectory))
            )
                yield return e;
    }

    public ValueResult<DirectoryContents> GetPathEntries(
        string path,
        bool includeHidden,
        bool includeOs
    )
    {
        try
        {
            List<DirectoryEntryInfo> dirs = new();
            List<FileEntryInfo> files = new();

            foreach (ISftpFile entry in ListDir(path, includeHidden))
            {
                if (entry.IsRegularFile || entry.IsSymbolicLink)
                    files.Add(MakeFileInfo(entry));
                if (entry.IsDirectory)
                    dirs.Add(MakeDirInfo(entry));
            }

            return new DirectoryContents { Dirs = dirs, Files = files }.OkResult();
        }
        catch (Exception ex)
        {
            return ValueResult<DirectoryContents>.Error(ex.Message);
        }
    }

    public async Task<ValueResult<DirectoryContents>> GetPathEntriesAsync(
        string path,
        bool includeHidden,
        bool includeOs,
        CancellationToken ct
    )
    {
        try
        {
            List<DirectoryEntryInfo> dirs = new();
            List<FileEntryInfo> files = new();

            await foreach (ISftpFile entry in ListDirAsync(path, includeHidden, ct))
            {
                if (entry.IsRegularFile || entry.IsSymbolicLink)
                    files.Add(MakeFileInfo(entry));
                if (entry.IsDirectory)
                    dirs.Add(MakeDirInfo(entry));
            }
            ct.ThrowIfCancellationRequested();

            return new DirectoryContents { Dirs = dirs, Files = files }.OkResult();
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException)
        {
            return ValueResult<DirectoryContents>.Error("Operation cancelled.");
        }
        catch (Exception ex)
        {
            return ValueResult<DirectoryContents>.Error(ex.Message);
        }
    }

    private static DirectoryEntryInfo MakeDirInfo(ISftpFile dir) =>
        new(dir.FullName, dir.Name, dir.LastWriteTime, dir.LastWriteTimeUtc);

    private static FileEntryInfo MakeFileInfo(ISftpFile file) =>
        new(
            file.FullName,
            file.Name,
            RemoteUnixPathTools.GetExtension(file.FullName),
            file.Length,
            file.LastWriteTime,
            file.LastWriteTimeUtc
        );

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

    public DateTime? GetFileLastWriteUtc(string filePath)
    {
        try
        {
            ISftpFile file = _client.Get(filePath);
            return ExistsInternal(file).AsFile ? file.LastWriteTimeUtc : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<DateTime?> GetFileLastWriteUtcAsync(string filePath)
    {
        try
        {
            ISftpFile file = await _client.GetAsync(filePath, CancellationToken.None);
            return ExistsInternal(file).AsFile ? file.LastWriteTimeUtc : null;
        }
        catch
        {
            return null;
        }
    }

    public DateTime? GetDirLastWriteUtc(string dirPath)
    {
        try
        {
            ISftpFile dir = _client.Get(dirPath);
            return ExistsInternal(dir).AsDir ? dir.LastWriteTimeUtc : null;
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
            return ExistsInternal(dir).AsDir ? dir.LastWriteTimeUtc : null;
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
