using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileSurfer.Models;

/// <summary>
/// Handles file and directory name validation and generation within <see cref="FileSurfer"/>.
/// </summary>
static class FileNameGenerator
{
    /// <summary>
    /// Finds a name available to use in <paramref name="directory"/> based on <paramref name="newName"/>.
    /// <para>
    /// Can start enumeration from <paramref name="lastIndex"/>, if specified.
    /// </para>
    /// </summary>
    /// <returns><see cref="string"/> name available to use in <paramref name="directory"/>.</returns>
    public static string GetAvailableName(string directory, string newName, int lastIndex = 0)
    {
        if (!Path.Exists(Path.Combine(directory, newName)))
            return newName;

        string nameWOextension = Path.GetFileNameWithoutExtension(newName);
        string extension = Path.GetExtension(newName);
        for (int index = lastIndex + 1; ; index++)
        {
            string newFileName = $"{nameWOextension} ({index}){extension}";
            if (!Path.Exists(Path.Combine(directory, newFileName)))
            {
                return newFileName;
            }
        }
    }

    /// <summary>
    /// Finds a name available for a copy in the context of 
    /// <see cref="ClipboardManager.Duplicate(string, out string[], out string?)"/> operation.
    /// </summary>
    /// <returns><see cref="string"/> name available to use in <paramref name="directory"/>.</returns>
    public static string GetCopyName(string directory, FileSystemEntry entry)
    {
        string extension = entry.IsDirectory ? string.Empty : Path.GetExtension(entry.PathToEntry);
        string copyName = entry.Name;
        if (extension != string.Empty)
            copyName = Path.GetFileNameWithoutExtension(entry.PathToEntry);

        return GetAvailableName(directory, copyName + " - Copy" + extension);
    }

    /// <summary>
    /// Determines if the files or directories represented by <paramref name="entries"/> can be collectively renamed.
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="entries"/> can be collectively renamed, otherwise <see langword="false"/>.</returns>
    public static bool CanBeRenamedCollectively(
        IEnumerable<FileSystemEntry> entries,
        bool onlyFiles,
        string extension
    )
    {
        foreach (FileSystemEntry entry in entries)
        {
            if (
                onlyFiles != !entry.IsDirectory
                || onlyFiles && Path.GetExtension(entry.PathToEntry) != extension
            )
                return false;
        }
        return true;
    }

    /// <summary>
    /// Gets new available name for the files represented by <paramref name="entries"/> accoring to <paramref name="namingPattern"/>.
    /// </summary>
    /// <returns>An array of names available for <paramref name="entries"/>.</returns>
    public static string[] GetAvailableNames(
        IEnumerable<FileSystemEntry> entries,
        string namingPattern
    )
    {
        string[] newNames = new string[entries.Count()];
        string extension = Path.GetExtension(namingPattern);
        string nameWOextension = Path.GetFileNameWithoutExtension(namingPattern);
        string directory =
            Path.GetDirectoryName(entries.First().PathToEntry)
            ?? throw new ArgumentException(entries.First().PathToEntry);

        int lastIndex = 1;
        for (int i = 0; i < newNames.Length; i++)
        {
            for (int index = lastIndex; ; index++)
            {
                string newFileName = $"{nameWOextension} ({index}){extension}";
                lastIndex++;

                if (!Path.Exists(Path.Combine(directory, newFileName)))
                {
                    newNames[i] = newFileName;
                    break;
                }
            }
        }
        return newNames;
    }
}
