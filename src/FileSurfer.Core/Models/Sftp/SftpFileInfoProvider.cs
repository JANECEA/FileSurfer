using System;
using System.Collections.Generic;
using FileSurfer.Core.Models.FileInformation;
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
    private readonly SftpClient _client;

    public SftpFileInfoProvider(SftpClient client) => _client = client;

    public bool IsLinkedToDirectory(string linkPath, out string? directory)
    {
        directory = null;
        try
        {
            if (!_client.Get(linkPath).IsSymbolicLink)
                return false;

            _ = _client.ListDirectory(linkPath); // Test
            directory = linkPath;
            return true;
        }
        catch
        {
            return false;
        }
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

    public string[] GetPathDirs(string path, bool includeHidden, bool includeOs)
    {
        DirCacheEntry cacheEntry = GetDirectoryEntriesCached(path);

        List<string> dirs = new(cacheEntry.Dirs.Count);
        foreach (ISftpFile entry in cacheEntry.Dirs)
            if (includeHidden || !IsHidden(entry.Name, true))
                dirs.Add(entry.FullName);

        return dirs.ToArray();
    }

    public string[] GetPathFiles(string path, bool includeHidden, bool includeOs)
    {
        DirCacheEntry cacheEntry = GetDirectoryEntriesCached(path);

        List<string> files = new(cacheEntry.Files.Count);
        foreach (ISftpFile entry in cacheEntry.Files)
            if (includeHidden || !IsHidden(entry.Name, true))
                files.Add(entry.FullName);

        return files.ToArray();
    }

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

    public DateTime? GetFileLastModified(string filePath)
    {
        try
        {
            ISftpFile file = _client.Get(filePath);
            return file.LastWriteTime;
        }
        catch
        {
            return null;
        }
    }

    public DateTime? GetDirLastModified(string dirPath)
    {
        try
        {
            ISftpFile dir = _client.Get(dirPath);
            return dir.LastWriteTime;
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
