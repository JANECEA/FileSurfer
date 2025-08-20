using System;
using System.IO;
using System.Runtime.InteropServices;
using Shell32;

namespace FileSurfer.Models.Shell;

/// <summary>
/// Interacts with the Windows <see cref="Shell"/> and <see cref="System.Runtime.InteropServices"/>
/// in order to restore files and directories from the system trash.
/// </summary>
public class WindowsFileRestorer : IFileRestorer
{
    private const int BinFolderID = 10;
    private const int NameColumn = 0;
    private const int PathColumn = 1;
    private const string RestoreVerb = "ESTORE";

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
}
