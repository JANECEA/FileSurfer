using System;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.Shell;
using FileSurfer.Core.Models.VersionControl;

namespace FileSurfer.Core.ViewModels;

public class FileSystemEntryVmFactory : IDisposable
{
    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly IIconProvider _iconProvider;
    private readonly IFileProperties _fileProperties;

    public FileSystemEntryVmFactory(
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
            VcStatus.NotVersionControlled
        );

    public FileSystemEntryViewModel Directory(string dirPath, VcStatus vcStatus) =>
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
            VcStatus.NotVersionControlled
        );

    public FileSystemEntryViewModel File(string filePath, VcStatus vcStatus) =>
        new(_fileInfoProvider, _fileProperties, _iconProvider, new FileEntry(filePath), vcStatus);

    public FileSystemEntryViewModel Drive(DriveEntry drive) =>
        new(_iconProvider, _fileProperties, drive);

    public void Dispose() => _iconProvider.Dispose();
}
