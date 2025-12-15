using System;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Linux.Models.FileInformation;

public class LinuxFileInfoProvider : IFileInfoProvider
{
    public DriveInfo[] GetDrives() =>
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
            .ToArray();

    public string[] GetPathFiles(
        string path,
        bool includeHidden,
        bool includeOS
    )
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
            return directories
                .Where(dirPath => dirPath != string.Empty)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public string[] GetSpecialFolders()
    {
        try
        {
            return new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    + "\\Downloads",
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments
                ),
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData
                ),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            };
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public bool TryGetFileIcon(string path, out Bitmap? bitmap)
    {
        bitmap = null;
        return false;
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
        try
        {
            return isDirectory
                ? new DirectoryInfo(path).Attributes.HasFlag(
                    FileAttributes.Hidden
                )
                : new FileInfo(path).Attributes.HasFlag(FileAttributes.Hidden);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsOSProtected(string path, bool isDirectory)
    {
        try
        {
            return isDirectory
                ? new DirectoryInfo(path).Attributes.HasFlag(
                    FileAttributes.System
                )
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
        return false;
    }
}
