using System;
using System.IO;
using System.Runtime.InteropServices;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.Shell;
using Microsoft.VisualBasic.FileIO;

namespace FileSurfer.Windows.Models.Shell;

/// <summary>
/// Interacts with the Windows <see cref="Shell"/> and <see cref="System.Runtime.InteropServices"/>
/// in order to restore files and directories from the system trash.
/// </summary>
public class WindowsBinInteraction : IBinInteraction
{
    private const int BinFolderID = 10;
    private const int NameColumn = 0;
    private const int PathColumn = 1;
    private const string RestoreVerb = "ESTORE";

    private readonly long _showDialogLimit;
    private readonly IFileInfoProvider _fileInfoProvider;

    public WindowsBinInteraction(long showDialogLimit, IFileInfoProvider fileInfoProvider)
    {
        _fileInfoProvider = fileInfoProvider;
        _showDialogLimit = showDialogLimit;
    }

    public IResult RestoreFile(string originalFilePath) => RestoreEntry(originalFilePath);

    public IResult RestoreDir(string originalDirPath) => RestoreEntry(originalDirPath);

    private static SimpleResult RestoreEntry(string originalPath)
    {
        Shell32.Shell shell = new();
        Folder bin = shell.NameSpace(BinFolderID);
        SimpleResult result = SimpleResult.Error($"Entry: \"{originalPath}\" not found.");
        try
        {
            foreach (FolderItem item in bin.Items())
            {
                string itemName = bin.GetDetailsOf(item, NameColumn);
                string itemPath = bin.GetDetailsOf(item, PathColumn);

                if (Path.Combine(itemPath, itemName) == originalPath)
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

    private static void DoVerb(FolderItem item, string verb)
    {
        foreach (FolderItemVerb verbObject in item.Verbs())
        {
            if (verbObject.Name.Contains(verb, StringComparison.CurrentCultureIgnoreCase))
            {
                verbObject.DoIt();
                return;
            }
        }
    }

    public IResult MoveFileToTrash(string filePath)
    {
        try
        {
            FileSystem.DeleteFile(
                filePath,
                _fileInfoProvider.GetFileSizeB(filePath) > _showDialogLimit
                    ? UIOption.AllDialogs
                    : UIOption.OnlyErrorDialogs,
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
                UIOption.AllDialogs,
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
}
