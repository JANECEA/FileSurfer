using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Linux.Models.FileInformation;

/// <summary>
/// Optimizes icon delivery on Linux using the mime-type.
/// </summary>
public class LinuxFileInfoProvider : IFileInfoProvider
{
    [
        SuppressMessage("ReSharper", "InconsistentNaming"),
        SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")
    ]
    private sealed record LsblkEntry(string? label, string? mountpoint, long size, string? type);

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private sealed record LsblkOutput(List<LsblkEntry> blockdevices);

    public DriveEntry[] GetDrives()
    {
        // To ShellHandler
        // TODO dependencies: lsblk
        ProcessStartInfo psi = new("lsblk", "-Jnbpo LABEL,MOUNTPOINT,SIZE,TYPE")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        List<DriveEntry> drives = new();
        LsblkOutput entries;
        try
        {
            Process? proc = Process.Start(psi);
            if (proc is null)
                return Array.Empty<DriveEntry>();

            string output = proc.StandardOutput.ReadToEnd();
            entries =
                JsonSerializer.Deserialize<LsblkOutput>(output) ?? throw new InvalidDataException();

            proc.WaitForExit();
        }
        catch
        {
            return Array.Empty<DriveEntry>();
        }
        foreach (LsblkEntry entry in entries.blockdevices)
            if (entry is { type: "part", label: not null, mountpoint: not null })
                drives.Add(new DriveEntry(entry.mountpoint, entry.label, entry.size));

        return drives.ToArray();
    }

    public string[] GetPathFiles(string path, bool includeHidden, bool includeOS)
    {
        try
        {
            string[] files = Directory.GetFiles(path);

            if (includeHidden && includeOS)
                return files;

            for (int i = 0; i < files.Length; i++)
            {
                if (
                    !includeHidden && IsHidden(files[i], false)
                    || !includeOS && IsOSProtected(files[i], false)
                )
                    files[i] = string.Empty;
            }
            return files.Where(filePath => filePath != string.Empty).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public string[] GetPathDirs(string path, bool includeHidden, bool includeOS)
    {
        try
        {
            string[] directories = Directory.GetDirectories(path);

            if (includeHidden && includeOS)
                return directories;

            for (int i = 0; i < directories.Length; i++)
            {
                if (
                    !includeHidden && IsHidden(directories[i], true)
                    || !includeOS && IsOSProtected(directories[i], true)
                )
                    directories[i] = string.Empty;
            }
            return directories.Where(dirPath => dirPath != string.Empty).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
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

    public DateTime? GetFileLastModified(string filePath)
    {
        try
        {
            return new FileInfo(filePath).LastWriteTime;
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
            return new DirectoryInfo(dirPath).LastWriteTime;
        }
        catch
        {
            return null;
        }
    }

    public bool IsHidden(string path, bool isDirectory)
    {
        int i = path.Length - 2;
        for (; i >= 0; i--)
            if (path[i] == PathTools.DirSeparator)
                break;

        return path[i + 1] == '.';
    }

    private static bool IsOSProtected(string path, bool isDirectory)
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

    public bool IsLinkedToDirectory(string linkPath, out string? directory)
    {
        directory = null;
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
