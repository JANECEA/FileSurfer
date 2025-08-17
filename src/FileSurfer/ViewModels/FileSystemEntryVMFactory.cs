using System.IO;
using FileSurfer.Models;
using FileSurfer.Models.FileInformation;
using FileSurfer.Models.VersionControl;

namespace FileSurfer.ViewModels;

public class FileSystemEntryVMFactory
{
    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly IIconProvider _iconProvider;

    public FileSystemEntryVMFactory(IFileInfoProvider fileInfoProvider, IIconProvider iconProvider)
    {
        _fileInfoProvider = fileInfoProvider;
        _iconProvider = iconProvider;
    }

    public FileSystemEntryViewModel CreateDirectory(
        string dirPath,
        VCStatus vcStatus = VCStatus.NotVersionControlled
    ) => new(_fileInfoProvider, _iconProvider, new DirectoryEntry(dirPath), vcStatus);

    public FileSystemEntryViewModel CreateFile(
        string filePath,
        VCStatus vcStatus = VCStatus.NotVersionControlled
    ) => new(_fileInfoProvider, _iconProvider, new FileEntry(filePath), vcStatus);

    public FileSystemEntryViewModel CreateDrive(DriveInfo driveInfo) =>
        new(_iconProvider, driveInfo);
}
