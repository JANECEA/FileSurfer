using System;
using System.Globalization;
using System.IO;
using System.Security;
using Avalonia.Controls;
using FileSurfer.Core.Models;
using FileSurfer.Core.ViewModels;
using FileSurfer.Linux.Models.Shell;
using Mono.Unix;

namespace FileSurfer.Linux.ViewModels;

public sealed class PropertiesVmFactory : IPropertiesVmFactory
{
    private readonly Window _parentWindow;

    public PropertiesVmFactory(Window mainWindow) => _parentWindow = mainWindow;

    private static string GetSize(FileSystemEntryViewModel entry, FileSystemInfo fsInfo)
    {
        if (!entry.IsDirectory)
        {
            if (entry.Size is [.., ' ', 'B'])
                return entry.Size;

            string bytesFormatted = (entry.SizeB ?? 0).ToString("N0", CultureInfo.InvariantCulture);
            return $"{entry.Size} ({bytesFormatted} B)";
        }

        DirectoryInfo dirInfo = (DirectoryInfo)fsInfo;
        try
        {
            int fileCount = dirInfo.GetFiles().Length;
            int dirCount = dirInfo.GetDirectories().Length;
            return $"{fileCount} files, {dirCount} sub-directories";
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
        {
            return "Permission error";
        }
    }

    private static string GetDateString(Func<DateTime> getDate)
    {
        try
        {
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            DateTimeFormatInfo formatInfo = cultureInfo.DateTimeFormat;
            string format = $"{formatInfo.LongDatePattern} {formatInfo.LongTimePattern}";

            return getDate().ToString(format, cultureInfo);
        }
        catch (IOException)
        {
            return "IO error";
        }
    }

    private static string GetOwner(string path)
    {
        UnixFileInfo fileInfo = new(path);

        string user =
            fileInfo.OwnerUser?.UserName
            ?? fileInfo.OwnerUserId.ToString(CultureInfo.InvariantCulture);
        string group =
            fileInfo.OwnerGroup?.GroupName
            ?? fileInfo.OwnerGroupId.ToString(CultureInfo.InvariantCulture);

        return user == group ? user : $"{user}:{group}";
    }

    private static string GetPermissions(string path)
    {
        UnixFileInfo fileInfo = new(path);
        FileAccessPermissions perms = fileInfo.FileAccessPermissions;

        char[] p = new char[9];

        p[0] = (perms & FileAccessPermissions.UserRead) != 0 ? 'r' : '-';
        p[1] = (perms & FileAccessPermissions.UserWrite) != 0 ? 'w' : '-';
        p[2] = (perms & FileAccessPermissions.UserExecute) != 0 ? 'x' : '-';

        p[3] = (perms & FileAccessPermissions.GroupRead) != 0 ? 'r' : '-';
        p[4] = (perms & FileAccessPermissions.GroupWrite) != 0 ? 'w' : '-';
        p[5] = (perms & FileAccessPermissions.GroupExecute) != 0 ? 'x' : '-';

        p[6] = (perms & FileAccessPermissions.OtherRead) != 0 ? 'r' : '-';
        p[7] = (perms & FileAccessPermissions.OtherWrite) != 0 ? 'w' : '-';
        p[8] = (perms & FileAccessPermissions.OtherExecute) != 0 ? 'x' : '-';

        return new string(p);
    }

    private static ValueResult<FileSystemInfo> GetFileSystemInfo(FileSystemEntryViewModel entry)
    {
        try
        {
            return ValueResult<FileSystemInfo>.Ok(
                entry.IsDirectory
                    ? new DirectoryInfo(entry.PathToEntry)
                    : new FileInfo(entry.PathToEntry)
            );
        }
        catch (Exception ex)
        {
            return ValueResult<FileSystemInfo>.Error(ex.Message);
        }
    }

    public ValueResult<IDisplayable> GetPropertiesVm(FileSystemEntryViewModel entry)
    {
        ValueResult<FileSystemInfo> result = GetFileSystemInfo(entry);
        if (!result.IsOk)
            return ValueResult<IDisplayable>.Error($"Could not be access \"{entry.Name}\".");

        FileSystemInfo fsInfo = result.Value;
        PropertiesWindowViewModel value = new(
            entry,
            _parentWindow,
            GetPermissions(entry.PathToEntry)
        )
        {
            Size = GetSize(entry, fsInfo),
            DateCreated = GetDateString(() => fsInfo.CreationTime),
            DateAccessed = GetDateString(() => fsInfo.LastAccessTime),
            DateModified = GetDateString(() => fsInfo.LastWriteTime),
            Owner = GetOwner(entry.PathToEntry),
        };
        return ValueResult<IDisplayable>.Ok(value);
    }
}
