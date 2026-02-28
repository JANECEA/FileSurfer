using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.Shell;

namespace FileSurfer.Linux.Models.FileInformation;

/// <summary>
/// Optimizes icon delivery on Linux using the mime-type.
/// </summary>
public class LinuxFileInfoProvider : ILocalFileInfoProvider
{
    private const string RootDir = "/";
    private readonly IShellHandler _shellHandler;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public IPathTools PathTools => LocalPathTools.Instance;

    public LinuxFileInfoProvider(IShellHandler shellHandler) => _shellHandler = shellHandler;

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private sealed record LsblkEntry(string? Label, string? MountPoint, long Size, string? Type);

    private sealed record LsblkOutput(List<LsblkEntry> BlockDevices);

    public DriveEntry[] GetDrives()
    {
        DriveEntry[] defaultList = [new(RootDir, "Root")];
        ValueResult<string> result = _shellHandler.ExecuteCommand(
            "lsblk",
            "-Jnbpo",
            "LABEL,MOUNTPOINT,SIZE,TYPE"
        );
        if (!result.IsOk || string.IsNullOrEmpty(result.Value))
            return Array.Empty<DriveEntry>();

        List<DriveEntry> drives = new();
        LsblkOutput entries;
        try
        {
            entries =
                JsonSerializer.Deserialize<LsblkOutput>(result.Value, _jsonSerializerOptions)
                ?? throw new InvalidDataException();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return defaultList;
        }
        foreach (LsblkEntry entry in entries.BlockDevices)
            if (entry is { Type: "part", Label: not null, MountPoint: not null, Size: > 0 })
                drives.Add(new DriveEntry(entry.MountPoint, entry.Label));

        return drives.Count > 0 ? drives.ToArray() : defaultList;
    }

    public ValueResult<List<FileEntryInfo>> GetPathFiles(
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

    public ValueResult<List<DirectoryEntryInfo>> GetPathDirs(
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

    public IEnumerable<string> GetSpecialFolders()
    {
        (Environment.SpecialFolder, string)[] folders =
        [
            (Environment.SpecialFolder.UserProfile, string.Empty),
            (Environment.SpecialFolder.UserProfile, "Downloads"),
            (Environment.SpecialFolder.MyDocuments, string.Empty),
            (Environment.SpecialFolder.MyPictures, string.Empty),
            (Environment.SpecialFolder.Desktop, string.Empty),
            (Environment.SpecialFolder.MyMusic, string.Empty),
            (Environment.SpecialFolder.MyVideos, string.Empty),
            (Environment.SpecialFolder.Templates, string.Empty),
        ];
        foreach ((Environment.SpecialFolder folder, string suffix) in folders)
        {
            string folderPath;
            try
            {
                folderPath = Path.Combine(Environment.GetFolderPath(folder), suffix);
            }
            catch
            {
                continue;
            }
            if (Directory.Exists(folderPath))
                yield return folderPath;
        }
    }

    public long GetFileSizeB(string path)
    {
        try
        {
            return new FileInfo(path).Length;
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
            return new FileInfo(filePath).LastWriteTimeUtc;
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
            return new DirectoryInfo(dirPath).LastWriteTimeUtc;
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
        while (i >= 0 && path[i] == LocalPathTools.DirSeparator)
            i--;

        for (; i >= 0; i--)
            if (path[i] == LocalPathTools.DirSeparator)
                break;

        return path[i + 1] == '.';
    }

    public string GetRoot() => RootDir;

    private static bool IsOsProtected(string path, bool isDirectory)
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

    public bool IsLinkedToDirectory(string linkPath, out string directory)
    {
        directory = null!;
        FileInfo fileInfo = new(linkPath);
        if (
            fileInfo.LinkTarget is null
            || fileInfo.ResolveLinkTarget(true) is not { } linkTarget
            || !linkTarget.Attributes.HasFlag(FileAttributes.Directory)
        )
            return false;

        directory = linkTarget.FullName;
        return true;
    }
}
