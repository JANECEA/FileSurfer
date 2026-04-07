using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileSurfer.Core;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Windows.Models.FileInformation;

public class WindowsFileInfoProvider : LocalFileInfoProvider
{
    public override DriveEntryInfo[] GetDrives() =>
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
            .Select(driveInfo => new DriveEntryInfo(driveInfo))
            .ToArray();

    public override IEnumerable<string> GetSpecialFolders()
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

    public override bool IsHidden(string path, bool isDirectory)
    {
        if (FileSurferSettings.TreatDotFilesAsHidden)
        {
            int i = path.Length - 2;
            for (; i >= 0; i--)
                if (
                    path[i] == LocalPathTools.DirSeparator
                    || path[i] == LocalPathTools.OtherSeparator
                )
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

    public override string GetRoot()
    {
        string dir = Directory.GetCurrentDirectory();
        return Path.GetPathRoot(dir)!;
    }

    public override bool IsLinkedToDirectory(string linkPath, out string directory)
    {
        directory = null!;
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
