using System;
using System.Runtime.InteropServices;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;
using Microsoft.VisualBasic.FileIO;
using FolderItem = Shell32.FolderItem;
using FolderItemVerb = Shell32.FolderItemVerb;

namespace FileSurfer.Windows.Services.Shell;

/// <summary>
/// Interacts with the Windows <see cref="Shell"/> and <see cref="System.Runtime.InteropServices"/>
/// in order to restore files and directories from the system trash.
/// </summary>
public class WindowsBinInteraction : IBinInteraction
{
    private const int BinFolderId = 10;
    private const string DeletedFromProperty = "System.Recycle.DeletedFrom";
    private const string RestoreVerb = "ESTORE";

    private readonly StaWorkerSync _workerSync = new("Bin worker thread");

    public IResult RestoreFile(string originalFilePath) =>
        _workerSync.Invoke(() => RestoreInternal(originalFilePath));

    public IResult RestoreDir(string originalDirPath) =>
        _workerSync.Invoke(() => RestoreInternal(originalDirPath));

    private static SimpleResult RestoreInternal(string originalPath)
    {
        Shell32.Shell shell = new();
        Shell32.Folder bin = shell.NameSpace(BinFolderId);
        SimpleResult result = SimpleResult.Error($"Entry: \"{originalPath}\" not found.");
        try
        {
            foreach (FolderItem item in bin.Items())
            {
                if (
                    TryGetOriginalPath(item, out string? itemOriginalPath)
                    && LocalPathTools.PathsAreEqual(itemOriginalPath, originalPath)
                )
                {
                    DoVerb(item, RestoreVerb);
                    result = SimpleResult.Ok();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            result = SimpleResult.Error(ex.Message);
        }
        Marshal.FinalReleaseComObject(bin);
        Marshal.FinalReleaseComObject(shell);

        return result;
    }

    private static bool TryGetOriginalPath(FolderItem item, out string? originalPath)
    {
        originalPath = null;
        try
        {
            dynamic comItem = item;
            string? deletedFrom = comItem.ExtendedProperty(DeletedFromProperty) as string;
            if (string.IsNullOrWhiteSpace(deletedFrom) || string.IsNullOrWhiteSpace(item.Name))
                return false;

            originalPath = LocalPathTools.NormalizePath(
                LocalPathTools.Combine(deletedFrom, item.Name)
            );
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void DoVerb(FolderItem item, string verb)
    {
        foreach (FolderItemVerb verbObject in item.Verbs())
            if (verbObject.Name.Contains(verb, StringComparison.CurrentCultureIgnoreCase))
            {
                verbObject.DoIt();
                return;
            }
    }

    public IResult MoveFileToTrash(string filePath)
    {
        try
        {
            FileSystem.DeleteFile(
                filePath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.ThrowException
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult MoveDirToTrash(string dirPath)
    {
        try
        {
            FileSystem.DeleteDirectory(
                dirPath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.ThrowException
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public void Dispose() => _workerSync.Dispose();
}
