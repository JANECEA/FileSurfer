using System;
using System.Collections.Generic;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.Sftp;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace FileSurfer.Core.Models.Sftp;

public sealed class SftpFileInfoProvider : IFileInfoProvider
{
    private sealed record DirCacheEntry(
        DateTime LastWriteTimeUtc,
        IReadOnlyList<ISftpFile> Files,
        IReadOnlyList<ISftpFile> Dirs
    );

    private readonly Dictionary<string, DirCacheEntry> _dirCache = new();
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

    private DirCacheEntry GetDirectoryEntriesCached(string path)
    {
        ISftpFile dirInfo = _client.Get(path);
        DateTime currentWriteTime = dirInfo.LastWriteTimeUtc;

        if (
            _dirCache.TryGetValue(path, out DirCacheEntry? cache)
            && cache.LastWriteTimeUtc >= currentWriteTime
        )
            return cache;

        List<ISftpFile> files = new();
        List<ISftpFile> dirs = new();

        foreach (ISftpFile entry in _client.ListDirectory(path))
            if (entry.Name is not ("." or ".."))
            {
                if (entry.IsRegularFile || entry.IsSymbolicLink)
                    files.Add(entry);
                else if (entry.IsDirectory)
                    dirs.Add(entry);
            }

        DirCacheEntry dirCacheEntry = new(currentWriteTime, files, dirs);
        _dirCache[path] = dirCacheEntry;
        return dirCacheEntry;
    }

    public ValueResult<List<DirectoryEntryInfo>> GetPathDirs(
        string path,
        bool includeHidden,
        bool includeOs
    )
    {
        DirCacheEntry cacheEntry;
        try
        {
            cacheEntry = GetDirectoryEntriesCached(path);
        }
        catch (Exception ex)
        {
            return ValueResult<List<DirectoryEntryInfo>>.Error(ex.Message);
        }

        List<DirectoryEntryInfo> dirs = new(cacheEntry.Dirs.Count);
        foreach (ISftpFile d in cacheEntry.Dirs)
            if (includeHidden || !IsHidden(d.Name, true))
                dirs.Add(
                    new DirectoryEntryInfo(d.FullName, d.Name, d.LastWriteTime, d.LastWriteTimeUtc)
                );

        return dirs.OkResult();
    }

    public ValueResult<List<FileEntryInfo>> GetPathFiles(
        string path,
        bool includeHidden,
        bool includeOs
    )
    {
        DirCacheEntry cacheEntry;
        try
        {
            cacheEntry = GetDirectoryEntriesCached(path);
        }
        catch (Exception ex)
        {
            return ValueResult<List<FileEntryInfo>>.Error(ex.Message);
        }

        List<FileEntryInfo> files = new(cacheEntry.Files.Count);
        foreach (ISftpFile file in cacheEntry.Files)
            if (includeHidden || !IsHidden(file.Name, true))
                files.Add(FromISftpFile(file));

        return files.OkResult();
    }

    private static FileEntryInfo FromISftpFile(ISftpFile file) =>
        new(
            file.FullName,
            file.Name,
            RemoteUnixPathTools.GetExtension(file.FullName),
            file.Length,
            file.LastWriteTime,
            file.LastWriteTimeUtc
        );

    public long GetFileSizeB(string path)
    {
        try
        {
            ISftpFile file = _client.Get(path);
            return file.Length;
        }
        catch
        {
            return 0;
        }
    }

    public DateTime? GetFileLastModifiedUtc(string filePath)
    {
        try
        {
            ISftpFile file = _client.Get(filePath);
            return file.LastWriteTimeUtc;
        }
        catch
        {
            return null;
        }
    }

    public DateTime? GetDirLastModifiedUtc(string dirPath)
    {
        try
        {
            ISftpFile dir = _client.Get(dirPath);
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

    public bool FileExists(string path)
    {
        try
        {
            ISftpFile file = _client.Get(path);
            return file.IsRegularFile || file.IsSymbolicLink;
        }
        catch
        {
            return false;
        }
    }

    public bool DirectoryExists(string path)
    {
        try
        {
            ISftpFile file = _client.Get(path);
            return file.IsDirectory;
        }
        catch
        {
            return false;
        }
    }

    public bool PathExists(string path)
    {
        try
        {
            return _client.Exists(path);
        }
        catch
        {
            return false;
        }
    }
}
