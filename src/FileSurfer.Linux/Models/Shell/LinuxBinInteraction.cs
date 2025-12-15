using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.Shell;

namespace FileSurfer.Linux.Models.Shell;

/// <summary>
/// Interacts with the Windows <see cref="Shell"/> and <see cref="System.Runtime.InteropServices"/>
/// in order to restore files and directories from the system trash.
/// </summary>
public class LinuxBinInteraction : IBinInteraction
{
    private const int BinFolderID = 10;
    private const int NameColumn = 0;
    private const int PathColumn = 1;
    private const string RestoreVerb = "ESTORE";

    private readonly long _showDialogLimit;
    private readonly IFileInfoProvider _fileInfoProvider;

    public LinuxBinInteraction(long showDialogLimit, IFileInfoProvider fileInfoProvider)
    {
        _fileInfoProvider = fileInfoProvider;
        _showDialogLimit = showDialogLimit;
    }

    public IResult RestoreFile(string originalFilePath) => RestoreEntry(originalFilePath);

    public IResult RestoreDir(string originalDirPath) => RestoreEntry(originalDirPath);

    private static SimpleResult RestoreEntry(string originalPath)
    {
        return SimpleResult.Error("Not implemented");
    }

    public IResult MoveFileToTrash(string filePath)
    {
        return SimpleResult.Error("Not implemented");
    }

    public IResult MoveDirToTrash(string dirPath)
    {
        return SimpleResult.Error("Not implemented");
    }
}
