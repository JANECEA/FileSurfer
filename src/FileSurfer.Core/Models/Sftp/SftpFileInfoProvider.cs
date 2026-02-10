using System;
using System.Collections.Generic;
using System.Linq;
using FileSurfer.Core.Models.FileInformation;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace FileSurfer.Core.Models.Sftp;

public sealed class SftpFileInfoProvider : IFileInfoProvider
{
    private sealed record DirectoryCacheEntry(DateTime LastWriteTimeUtc, ISftpFile[] Entries);

    private readonly Dictionary<string, DirectoryCacheEntry> _dirCache = new();
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

    private ISftpFile[] GetDirectoryEntriesCached(string path)
    {
        ISftpFile dirInfo = _client.Get(path);
        DateTime currentWriteTime = dirInfo.LastWriteTimeUtc;

        if (
            _dirCache.TryGetValue(path, out DirectoryCacheEntry? cache)
            && cache.LastWriteTimeUtc >= currentWriteTime
        )
            return cache.Entries;

        ISftpFile[] entries = _client.ListDirectory(path).ToArray();
        _dirCache[path] = new DirectoryCacheEntry(currentWriteTime, entries);

        return entries;
    }

    public string[] GetPathDirs(string path, bool includeHidden, bool includeOs)
    {
        ISftpFile[] entries = GetDirectoryEntriesCached(path);

        List<string> dirs = new(entries.Length);
        foreach (ISftpFile entry in entries)
            if (
                entry.Name is not ("." or "..")
                && entry.IsDirectory
                && (includeHidden || !IsHidden(entry.Name, true))
            )
                dirs.Add(entry.FullName);

        return dirs.ToArray();
    }

    public string[] GetPathFiles(string path, bool includeHidden, bool includeOs)
    {
        ISftpFile[] entries = GetDirectoryEntriesCached(path);

        List<string> files = new();
        foreach (ISftpFile entry in entries)
            if (
                entry.Name is not ("." or "..")
                && entry.IsRegularFile
                && (includeHidden || !IsHidden(entry.Name, false))
            )
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
