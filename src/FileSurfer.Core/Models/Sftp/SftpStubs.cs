using System.Collections.Generic;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileOperations;

public class SftpArchiveManager : IArchiveManager
{
    public bool IsZipped(string filePath) => false;

    public IResult ZipFiles(
        IEnumerable<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName
    ) => SimpleResult.Error("Unsupported platform");

    public IResult UnzipArchive(string archivePath, string destinationPath) =>
        SimpleResult.Error("Unsupported platform");
}
