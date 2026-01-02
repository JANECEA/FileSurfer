using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileSurfer.Core;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Windows.Models.FileInformation;

public class WindowsFileInfoProvider : IFileInfoProvider
{
    public DriveEntry[] GetDrives() =>
        DriveInfo
            .GetDrives()
            .Where(static drive =>
            {
                try
                {
                    // For some drives retrieving these essential values throws an exception
                    // In that case they are skipped.
                    _ = $"{drive.Name}{drive.VolumeLabel}{drive.TotalSize}";
                    return drive.IsReady;
                }
                catch
                {
                    return false;
                }
            })
            .Select(driveInfo => new DriveEntry(driveInfo))
            .ToArray();

    public string[] GetPathFiles(string path, bool includeHidden, bool includeOs)
    {
        try
        {
            string[] files = Directory.GetFiles(path);

            if (includeHidden && includeOs)
                return files;

            for (int i = 0; i < files.Length; i++)
            {
                if (
                    !includeHidden && IsHidden(files[i], false)
                    || !includeOs && IsOsProtected(files[i], false)
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

    public string[] GetPathDirs(string path, bool includeHidden, bool includeOs)
    {
        try
        {
            string[] directories = Directory.GetDirectories(path);

            if (includeHidden && includeOs)
                return directories;

            for (int i = 0; i < directories.Length; i++)
            {
                if (
                    !includeHidden && IsHidden(directories[i], true)
                    || !includeOs && IsOsProtected(directories[i], true)
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
            (Environment.SpecialFolder.Desktop, string.Empty),
            (Environment.SpecialFolder.UserProfile, "Downloads"),
            (Environment.SpecialFolder.MyDocuments, string.Empty),
            (Environment.SpecialFolder.ApplicationData, string.Empty),
            (Environment.SpecialFolder.MyPictures, string.Empty),
            (Environment.SpecialFolder.MyMusic, string.Empty),
            (Environment.SpecialFolder.MyVideos, string.Empty),
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
        if (FileSurferSettings.TreatDotFilesAsHidden)
        {
            int i = path.Length - 2;
            for (; i >= 0; i--)
                if (path[i] == PathTools.DirSeparator)
                    break;

            if (path[i + 1] == '.')
                return true;
        }
        try
        {
            return isDirectory
                ? new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.Hidden)
                : new FileInfo(path).Attributes.HasFlag(FileAttributes.Hidden);
        }
        catch
        {
            return false;
        }
    }

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

    public bool IsLinkedToDirectory(string linkPath, out string? directory)
    {
        directory = null;
        if (!Path.GetExtension(linkPath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            IWshRuntimeLibrary.WshShell shell = new();
            IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)
                shell.CreateShortcut(linkPath);

            if (Directory.Exists(shortcut.TargetPath))
            {
                directory = shortcut.TargetPath;
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
