using System;
using System.Globalization;
using System.IO;
using System.Security;
using Avalonia.Controls;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Linux.ViewModels;

/// <summary>
/// Factory to create properties window view-model
/// </summary>
public interface IPropertiesVmFactory
{
    /// <summary>
    /// Creates the Properties window view-model
    /// </summary>
    /// <param name="entry">The underlying entry</param>
    /// <param name="info">Info for the file system entry</param>
    /// <param name="permissions">Unix permissions in <c>rwxrwxrwx</c> format</param>
    /// <param name="owner">Name of the owner of the file system entry</param>
    public IDisplayable GetPropertiesVm(
        FileSystemEntryViewModel entry,
        FileSystemInfo info,
        string permissions,
        string owner
    );
}

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

    public IDisplayable GetPropertiesVm(
        FileSystemEntryViewModel entry,
        FileSystemInfo info,
        string permissions,
        string owner
    )
    {
        PropertiesWindowViewModel value = new(entry, _parentWindow, permissions)
        {
            Size = GetSize(entry, info),
            DateCreated = GetDateString(() => info.CreationTime),
            DateAccessed = GetDateString(() => info.LastAccessTime),
            DateModified = GetDateString(() => info.LastWriteTime),
            Owner = owner,
        };
        return value;
    }
}
