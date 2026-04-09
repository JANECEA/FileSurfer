using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Extensions;

// ReSharper disable VirtualMemberNeverOverridden.Global

namespace FileSurfer.Core.Models.FileInformation;

/// <summary>
/// Implements cross-platform methods for local Linux
/// and Windows file systems.
/// </summary>
public abstract class LocalFileInfoProvider : ILocalFileInfoProvider
{
    public virtual IPathTools PathTools => LocalPathTools.Instance;

    public abstract bool IsLinkedToDirectory(
        string linkPath,
        [NotNullWhen(true)] out string? directory
    );

    public ValueResult<DirectoryContents> GetPathEntries(
        string path,
        bool includeHidden,
        bool includeOs
    )
    {
        try
        {
            IEnumerable<FileSystemInfo> entries = new DirectoryInfo(
                path
            ).EnumerateFileSystemInfos();
            if (!includeHidden)
                entries = entries.Where(e => !IsHidden(e.FullName, e is DirectoryInfo));
            if (!includeOs)
                entries = entries.Where(e => !IsOsProtected(e.FullName, e is DirectoryInfo));

            List<DirectoryEntryInfo> dirs = new();
            List<FileEntryInfo> files = new();

            foreach (FileSystemInfo entry in entries)
            {
                if (entry is FileInfo f)
                    files.Add(new FileEntryInfo(f));
                if (entry is DirectoryInfo d)
                    dirs.Add(new DirectoryEntryInfo(d));
            }

            return new DirectoryContents { Files = files, Dirs = dirs }.OkResult();
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
            IEnumerable<FileSystemInfo> entries = new DirectoryInfo(
                path
            ).EnumerateFileSystemInfos();
            if (!includeHidden)
                entries = entries.Where(e => !IsHidden(e.FullName, e is DirectoryInfo));
            if (!includeOs)
                entries = entries.Where(e => !IsOsProtected(e.FullName, e is DirectoryInfo));

            (List<DirectoryEntryInfo> dirs, List<FileEntryInfo> files) = await Task.Run(
                () =>
                {
                    List<DirectoryEntryInfo> dirs = new();
                    List<FileEntryInfo> files = new();
                    foreach (FileSystemInfo entry in entries)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (entry is FileInfo f)
                            files.Add(new FileEntryInfo(f));
                        if (entry is DirectoryInfo d)
                            dirs.Add(new DirectoryEntryInfo(d));
                    }
                    return (dirs, files);
                },
                ct
            );

            return new DirectoryContents { Files = files, Dirs = dirs }.OkResult();
        }
        catch (Exception ex)
        {
            return ValueResult<DirectoryContents>.Error(ex.Message);
        }
    }

    public virtual ValueResult<Stream> GetFileStream(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read).OkResult<Stream>();
        }
        catch (Exception ex)
        {
            return ValueResult<Stream>.Error(ex.Message);
        }
    }

    public Task<DateTime?> GetFileLastWriteUtcAsync(string filePath)
    {
        DateTime? time;
        try
        {
            time = File.GetLastWriteTimeUtc(filePath);
        }
        catch
        {
            time = null;
        }
        return Task.FromResult(time);
    }

    public virtual Task<DateTime?> GetDirLastWriteUtcAsync(string dirPath)
    {
        DateTime? time;
        try
        {
            time = Directory.GetLastWriteTimeUtc(dirPath);
        }
        catch
        {
            time = null;
        }
        return Task.FromResult(time);
    }

    protected virtual bool IsOsProtected(string path, bool isDirectory)
    {
        try
        {
            return isDirectory
                ? new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.System)
                : new FileInfo(path).Attributes.HasFlag(FileAttributes.System);
        }
        catch
        {
            return true;
        }
    }

    public abstract bool IsHidden(string path, bool isDirectory);

    public abstract string GetRoot();

    public ExistsInfo Exists(string path)
    {
        if (Directory.Exists(path))
            return ExistsInfo.ExistsAsDirectory();

        if (File.Exists(path))
            return ExistsInfo.ExistsAsFile();

        return ExistsInfo.DoesNotExist();
    }

    public Task<ExistsInfo> ExistsAsync(string path) => Task.FromResult(Exists(path));

    public abstract DriveEntryInfo[] GetDrives();

    public abstract IEnumerable<string> GetSpecialFolders();
}
