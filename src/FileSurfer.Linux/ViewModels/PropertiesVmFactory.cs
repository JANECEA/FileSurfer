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
            return $"{entry.Size} ({entry.SizeB ?? 0}B)";

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
            return getDate().ToString(CultureInfo.CurrentCulture);
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

    private static string FormatPermissions(FileAccessPermissions perms)
    {
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

    private static string GetPermissions(string path)
    {
        UnixFileInfo fileInfo = new(path);
        FileAccessPermissions perms = fileInfo.FileAccessPermissions;

        return FormatPermissions(perms);
    }

    public ValueResult<IDisplayable> GetPropertiesVm(FileSystemEntryViewModel entry)
    {
        FileSystemInfo fsInfo;
        try
        {
            fsInfo = entry.IsDirectory
                ? new DirectoryInfo(entry.PathToEntry)
                : new FileInfo(entry.PathToEntry);
        }
        catch (Exception ex)
        {
            return ValueResult<IDisplayable>.Error(ex.Message);
        }

        if (!fsInfo.Exists)
            return ValueResult<IDisplayable>.Error($"{entry.PathToEntry} does not exist.");

        PropertiesWindowViewModel value = new(entry, _parentWindow)
        {
            Size = GetSize(entry, fsInfo),
            DateCreated = GetDateString(() => fsInfo.CreationTime),
            DateAccessed = GetDateString(() => fsInfo.LastAccessTime),
            DateModified = GetDateString(() => fsInfo.LastWriteTime),
            Owner = GetOwner(entry.PathToEntry),
            Permissions = GetPermissions(entry.PathToEntry),
        };
        return ValueResult<IDisplayable>.Ok(value);
    }
}
