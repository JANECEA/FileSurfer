using System;
using System.IO;
using System.Runtime.InteropServices;
using Shell32;

namespace FileSurfer.Models;

/// <summary>
/// Interacts with the Windows <see cref="Shell"/> and <see cref="System.Runtime.InteropServices"/>
/// in order to restore files and directories from the system trash.
/// </summary>
static class WindowsFileRestorer
{
    private const int BinFolderID = 10;
    private const int NameColumn = 0;
    private const int PathColumn = 1;
    private const string RestoreVerb = "ESTORE";

    /// <summary>
    /// Restores a file or a directory based on <paramref name="originalPath"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the operation was succesfull, otherwise <see langword="false"/>.</returns>
    public static bool RestoreEntry(string originalPath, out string? errorMessage)
    {
        Shell shell = new();
        errorMessage = null;
        Folder bin = shell.NameSpace(BinFolderID);
        bool entryFound = false;
        try
        {
            foreach (FolderItem item in bin.Items())
            {
                string itemName = bin.GetDetailsOf(item, NameColumn);
                string itemPath = bin.GetDetailsOf(item, PathColumn);

                if (Path.Combine(itemPath, itemName) == originalPath)
                {
                    DoVerb(item, RestoreVerb);
                    entryFound = true;
                    break;
                }
            }
            errorMessage = entryFound ? null : $"Entry: \"{originalPath}\" not found.";
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        Marshal.FinalReleaseComObject(bin);
        Marshal.FinalReleaseComObject(shell);
        return entryFound;
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
