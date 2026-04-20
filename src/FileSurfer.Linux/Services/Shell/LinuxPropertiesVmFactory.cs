using System.Globalization;
using System.IO;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Linux.Services.Shell;

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

/// <summary>
/// Linux implementation of <see cref="IPropertiesVmFactory"/>.
/// </summary>
public sealed class LinuxPropertiesVmFactory : IPropertiesVmFactory
{
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
        catch
        {
            return "_";
        }
    }

    public IDisplayable GetPropertiesVm(
        FileSystemEntryViewModel entry,
        FileSystemInfo info,
        string permissions,
        string owner
    )
    {
        PropertiesWindowViewModel value = new(entry, permissions)
        {
            Size = GetSize(entry, info),
            DateCreated = info.CreationTime,
            DateAccessed = info.LastAccessTime,
            DateModified = info.LastWriteTime,
            Owner = owner,
        };
        return value;
    }
}
