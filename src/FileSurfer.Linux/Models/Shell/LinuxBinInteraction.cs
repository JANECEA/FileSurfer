using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Shell;

namespace FileSurfer.Linux.Models.Shell;

/// <summary>
/// Interacts with the <c>gio trash</c> cli utility in order to move/restore files and directories to/from the system trash.
/// </summary>
public class LinuxBinInteraction : IBinInteraction
{
    // TODO dependencies: gio, gvfs, gvfs-client, gvfs-fuse
    private readonly IShellHandler _shellHandler;

    public LinuxBinInteraction(IShellHandler shellHandler) => _shellHandler = shellHandler;

    public IResult RestoreFile(string originalFilePath) => RestoreEntry(originalFilePath);

    public IResult RestoreDir(string originalDirPath) => RestoreEntry(originalDirPath);

    private IResult RestoreEntry(string originalPath)
    {
        // TODO implement when ExecuteCmd returns STDOUT
        return SimpleResult.Error("Not implemented");
    }

    public IResult MoveFileToTrash(string filePath) => MoveEntryToTrash(filePath);

    public IResult MoveDirToTrash(string dirPath) => MoveEntryToTrash(dirPath);

    private IResult MoveEntryToTrash(string path) =>
        _shellHandler.ExecuteCmd($"gio trash \"{path}\"");
}
