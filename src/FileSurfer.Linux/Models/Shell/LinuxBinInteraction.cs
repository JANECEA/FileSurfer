using System.IO;
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
        ValueResult<string> result = _shellHandler.ExecuteCommand("gio", "trash --list");
        if (!result.IsOk)
            return SimpleResult.Error("Could not access system trash");

        StringReader reader = new(result.Value);
        string? trashUri = GetTrashUri(reader, originalPath);

        if (trashUri is null && _shellHandler.ExecuteCommand("killall", "gvfsd-trash").IsOk)
        {
            result = _shellHandler.ExecuteCommand("gio", "trash --list");
            if (!result.IsOk)
                return SimpleResult.Error("Restart did not succeed.");

            if (string.IsNullOrEmpty(result.Value))
                return SimpleResult.Error("Trash is empty");

            reader = new StringReader(result.Value);
            trashUri = GetTrashUri(reader, originalPath);
        }

        return trashUri is null
            ? SimpleResult.Error("Could not find path in system trash.")
            : _shellHandler.ExecuteCommand("gio", $"trash --restore {trashUri}");
    }

    private static string? GetTrashUri(StringReader reader, string originalTrashedPath)
    {
        originalTrashedPath = PathTools.NormalizePath(originalTrashedPath);

        int largestIndex = int.MinValue;
        string? largestIndexUri = null;
        while (reader.ReadLine() is { } line)
        {
            int tabIndex = line.IndexOf('\t');
            if (tabIndex == -1)
                continue;

            string origPath = line[(tabIndex + 1)..];
            if (!PathTools.PathsAreEqualNormalized(origPath, originalTrashedPath))
                continue;

            string trashUri = line[..tabIndex];
            int index = GetUriIndex(trashUri);
            if (index > largestIndex)
            {
                largestIndex = index;
                largestIndexUri = trashUri;
            }
        }
        return largestIndexUri;
    }

    private static int GetUriIndex(string trashItemUri)
    {
        string? extension = PathTools.GetExtensionNoDot(trashItemUri);
        if (string.IsNullOrEmpty(extension))
            return 0;

        int dotIndex = extension.IndexOf('.');
        string numberPart = dotIndex != -1 ? extension[..dotIndex] : extension;

        return uint.TryParse(numberPart, out uint number) ? (int)number : 0;
    }

    public IResult MoveFileToTrash(string filePath) => MoveEntryToTrash(filePath);

    public IResult MoveDirToTrash(string dirPath) => MoveEntryToTrash(dirPath);

    private ValueResult<string> MoveEntryToTrash(string path) =>
        _shellHandler.ExecuteCommand("gio", $"trash \"{path}\"");
}
