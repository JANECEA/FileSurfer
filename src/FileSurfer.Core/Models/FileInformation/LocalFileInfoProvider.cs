using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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

    public virtual ValueResult<List<DirectoryEntryInfo>> GetPathDirs(
        string path,
        bool includeHidden,
        bool includeOs
    )
    {
        try
        {
            DirectoryInfo[] dirs = new DirectoryInfo(path).GetDirectories();
            List<DirectoryEntryInfo> dirsList = new(dirs.Length);

            foreach (DirectoryInfo d in dirs)
                if (
                    (includeHidden || !IsHidden(d.FullName, true))
                    && (includeOs || !IsOsProtected(d.FullName, true))
                )
                    dirsList.Add(new DirectoryEntryInfo(d));

            return dirsList.OkResult();
        }
        catch (Exception ex)
        {
            return ValueResult<List<DirectoryEntryInfo>>.Error(ex.Message);
        }
    }

    public virtual ValueResult<List<FileEntryInfo>> GetPathFiles(
        string path,
        bool includeHidden,
        bool includeOs
    )
    {
        try
        {
            FileInfo[] files = new DirectoryInfo(path).GetFiles();
            List<FileEntryInfo> fileList = new(files.Length);

            foreach (FileInfo f in files)
                if (
                    (includeHidden || !IsHidden(f.FullName, false))
                    && (includeOs || !IsOsProtected(f.FullName, false))
                )
                    fileList.Add(new FileEntryInfo(f));

            return fileList.OkResult();
        }
        catch (Exception ex)
        {
            return ValueResult<List<FileEntryInfo>>.Error(ex.Message);
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
