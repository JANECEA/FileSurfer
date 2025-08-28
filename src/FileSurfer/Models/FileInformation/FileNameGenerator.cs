using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileSurfer.Models.FileOperations;

namespace FileSurfer.Models.FileInformation;

/// <summary>
/// Handles file and directory name validation and generation within the <see cref="FileSurfer"/> app.
/// </summary>
internal static class FileNameGenerator
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

        string nameWithoutExtension = Path.GetFileNameWithoutExtension(newName);
        string extension = Path.GetExtension(newName);
        for (int index = lastIndex + 1; ; index++)
        {
            string newFileName = $"{nameWithoutExtension} ({index}){extension}";

            if (!Path.Exists(Path.Combine(directory, newFileName)))
                return newFileName;
        }
    }

    /// <summary>
    /// Finds a name available for a copy in the context of
    /// <see cref="ClipboardManager.Duplicate(string, out string[])"/> operation.
    /// </summary>
    /// <returns>Name of a copy, available to use in the path specified in: <paramref name="directory"/>.</returns>
    public static string GetCopyName(string directory, IFileSystemEntry entry) =>
        GetAvailableName(directory, $"{entry.NameWOExtension} - Copy{entry.Extension}");

    /// <summary>
    /// Determines if the files or directories represented by <paramref name="entries"/> can be collectively renamed.
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="entries"/> can be collectively renamed, otherwise <see langword="false"/>.</returns>
    public static bool CanBeRenamedCollectively(IList<IFileSystemEntry> entries)
    {
        if (entries.Count < 2)
            return true;

        bool onlyFiles = entries[0] is FileEntry;
        string extension = entries[0].Extension;

        for (int i = 1; i < entries.Count; i++)
            if (
                entries[i] is FileEntry != onlyFiles
                || !string.Equals(entries[i].Extension, extension, StringComparison.OrdinalIgnoreCase)
            )
                return false;

        return true;
    }

    /// <summary>
    /// Gets new available name for the files represented by <paramref name="entries"/> accoring to <paramref name="namingPattern"/>.
    /// </summary>
    /// <returns>An array of names available for <paramref name="entries"/>.</returns>
    public static string[] GetAvailableNames(IList<IFileSystemEntry> entries, string namingPattern)
    {
        string[] newNames = new string[entries.Count];
        string extension = Path.GetExtension(namingPattern);
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(namingPattern);
        string directory =
            Path.GetDirectoryName(entries.First().PathToEntry)
            ?? throw new ArgumentException(entries.First().PathToEntry);

        int lastIndex = 1;
        for (int i = 0; i < newNames.Length; i++)
        {
            for (int index = lastIndex; ; index++)
            {
                string newFileName = $"{nameWithoutExtension} ({index}){extension}";
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
