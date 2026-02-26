using System.IO;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;

namespace FileSurfer.Linux.Services.Shell;

/// <summary>
/// Interacts with the <c>gio trash</c> cli utility in order to move/restore files and directories to/from the system trash.
/// </summary>
public class LinuxBinInteraction : IBinInteraction
{
    private readonly IShellCommandHandler _shellHandler;

    public LinuxBinInteraction(IShellCommandHandler shellHandler) => _shellHandler = shellHandler;

    public IResult RestoreFile(string originalFilePath) => RestoreEntry(originalFilePath);

    public IResult RestoreDir(string originalDirPath) => RestoreEntry(originalDirPath);

    private IResult RestoreEntry(string originalPath)
    {
        ValueResult<string> result = _shellHandler.ExecuteCommand("trash-list");
        if (!result.IsOk)
            return SimpleResult.Error("Failed to access system trash.");

        if (string.IsNullOrEmpty(result.Value))
            return SimpleResult.Error("Trash list is empty.");

        int newestIndex = GetNewestIndex(result.Value, originalPath);
        if (newestIndex < 0)
            return SimpleResult.Error($"Could not find \"{originalPath}\" in trash.");

        return _shellHandler.ExecuteShellCommand(
            $"echo {newestIndex} | trash-restore \"$1\"",
            originalPath
        );
    }

    private static int GetNewestIndex(string stdOut, string originalPath)
    {
        originalPath = LocalPathTools.NormalizePath(originalPath);
        StringReader reader = new(stdOut);

        int index = -1;
        while (reader.ReadLine() is string line)
        {
            int secondSpaceIdx = line.IndexOf(' ', line.IndexOf(' ') + 1);
            string path = line[(secondSpaceIdx + 1)..];

            if (
                LocalPathTools.PathsAreEqualNormalized(
                    LocalPathTools.NormalizePath(path),
                    originalPath
                )
            )
                index++;
        }
        return index;
    }

    public IResult MoveFileToTrash(string filePath) => MoveEntryToTrash(filePath);

    public IResult MoveDirToTrash(string dirPath) => MoveEntryToTrash(dirPath);

    private ValueResult<string> MoveEntryToTrash(string path) =>
        _shellHandler.ExecuteCommand("trash-put", path);
}
