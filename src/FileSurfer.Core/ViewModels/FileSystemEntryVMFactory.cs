using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.Shell;
using FileSurfer.Core.Models.VersionControl;

namespace FileSurfer.Core.ViewModels;

public class FileSystemEntryVMFactory
{
    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly IIconProvider _iconProvider;
    private readonly IFileProperties _fileProperties;

    public FileSystemEntryVMFactory(
        IFileInfoProvider fileInfoProvider,
        IFileProperties fileProperties,
        IIconProvider iconProvider
    )
    {
        _fileInfoProvider = fileInfoProvider;
        _fileProperties = fileProperties;
        _iconProvider = iconProvider;
    }

    public FileSystemEntryViewModel Directory(string dirPath) =>
        new(
            _fileInfoProvider,
            _fileProperties,
            _iconProvider,
            new DirectoryEntry(dirPath),
            VCStatus.NotVersionControlled
        );

    public FileSystemEntryViewModel Directory(string dirPath, VCStatus vcStatus) =>
        new(
            _fileInfoProvider,
            _fileProperties,
            _iconProvider,
            new DirectoryEntry(dirPath),
            vcStatus
        );

    public FileSystemEntryViewModel File(string filePath) =>
        new(
            _fileInfoProvider,
            _fileProperties,
            _iconProvider,
            new FileEntry(filePath),
            VCStatus.NotVersionControlled
        );

    public FileSystemEntryViewModel File(string filePath, VCStatus vcStatus) =>
        new(_fileInfoProvider, _fileProperties, _iconProvider, new FileEntry(filePath), vcStatus);

    public FileSystemEntryViewModel Drive(DriveEntry drive) =>
        new(_iconProvider, _fileProperties, drive);
}
