using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileSurfer.Core;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Windows.Models.FileInformation;

public class WindowsFileInfoProvider : ILocalFileInfoProvider
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
                    (includeHidden || !IsHidden(d.FullName, false))
                    && (includeOs || !IsOsProtected(d.FullName, false))
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
        if (FileSurferSettings.TreatDotFilesAsHidden)
        {
            int i = path.Length - 2;
            for (; i >= 0; i--)
                if (path[i] == LocalPathTools.DirSeparator)
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

    public string GetRoot()
    {
        string dir = Directory.GetCurrentDirectory();
        return Path.GetPathRoot(dir)!;
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
