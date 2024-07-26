using Shell32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FileSurfer;

static class WindowsFileRestorer
{
    private const int BinFolderID = 10;
    private const int NameColumn = 0;
    private const int PathColumn = 1;
    private const string RestoreVerb = "ESTORE";

    public static bool RestoreFile(string originalPath, out string? errorMessage)
    {
        Shell shell = new();
        errorMessage = null;
        try 
        {
            Folder bin = shell.NameSpace(BinFolderID);
            foreach (FolderItem item in bin.Items())
            {
                string itemName = bin.GetDetailsOf(item, NameColumn);
                string itemPath = bin.GetDetailsOf(item, PathColumn);

                if (Path.Combine(itemPath, itemName) == originalPath)
                {
                    DoVerb(item, RestoreVerb);
                    Marshal.FinalReleaseComObject(shell);
                    return true;
                }
            }
            Marshal.FinalReleaseComObject(shell);
            errorMessage = $"file: \"{originalPath}\" not found";
            return false; 
        }
        catch (Exception ex) 
        {
            errorMessage = ex.Message;
        }
        finally 
        {
            Marshal.FinalReleaseComObject(shell);
        }
        return false;
    }

    public static bool RestoreDir(string originalPath, out string? errorMessage)
    {
        Shell shell = new();
        Folder recycleBin = shell.NameSpace(BinFolderID);
        StringBuilder errors = new();
        foreach (FolderItem item in recycleBin.Items())
        {
            try
            {
                string itemPath = recycleBin.GetDetailsOf(item, PathColumn);

                if (itemPath.StartsWith(originalPath))
                {
                    DoVerb(item, RestoreVerb);
                }
            }
            catch
            {
                errors.Append(recycleBin.GetDetailsOf(item, NameColumn));
            }
        }
        Marshal.FinalReleaseComObject(shell);
        errorMessage = errors.Length == 0 ? null : $"Error processing these files: {errors}";
        return errors.Length == 0;
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
