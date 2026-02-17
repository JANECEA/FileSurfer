using System;
using System.Globalization;
using System.IO;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.ViewModels;
using FileSurfer.Linux.ViewModels;
using Mono.Unix;

namespace FileSurfer.Linux.Services.Shell;

public class LinuxFileProperties : IFileProperties
{
    private readonly IPropertiesVmFactory _vmFactory;

    public LinuxFileProperties(IPropertiesVmFactory vmFactory) => _vmFactory = vmFactory;

    public IResult ShowFileProperties(FileSystemEntryViewModel entry)
    {
        ValueResult<FileSystemInfo> infoResult = GetFileSystemInfo(entry);
        if (!infoResult.IsOk)
            return infoResult;

        UnixFileInfo unixInfo = new(entry.PathToEntry);
        IDisplayable propertiesVm = _vmFactory.GetPropertiesVm(
            entry,
            infoResult.Value,
            GetPermissions(unixInfo),
            GetOwner(unixInfo)
        );
        propertiesVm.Show();
        return SimpleResult.Ok();
    }

    private static ValueResult<FileSystemInfo> GetFileSystemInfo(FileSystemEntryViewModel entry)
    {
        try
        {
            FileSystemInfo info = entry.IsDirectory
                ? new DirectoryInfo(entry.PathToEntry)
                : new FileInfo(entry.PathToEntry);
            return info.OkResult();
        }
        catch (Exception ex)
        {
            return ValueResult<FileSystemInfo>.Error(ex.Message);
        }
    }

    private static string GetOwner(UnixFileInfo unixInfo)
    {
        string user =
            unixInfo.OwnerUser?.UserName
            ?? unixInfo.OwnerUserId.ToString(CultureInfo.InvariantCulture);
        string group =
            unixInfo.OwnerGroup?.GroupName
            ?? unixInfo.OwnerGroupId.ToString(CultureInfo.InvariantCulture);

        return user == group ? user : $"{user}:{group}";
    }

    private static string GetPermissions(UnixFileInfo unixInfo)
    {
        FileAccessPermissions perms = unixInfo.FileAccessPermissions;
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

    public bool SupportsOpenAs(IFileSystemEntry entry) => false;

    public IResult ShowOpenAsDialog(IFileSystemEntry entry) =>
        SimpleResult.Error("Unsupported operating system");
}
