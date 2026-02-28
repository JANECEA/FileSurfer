using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileSurfer.Core.Models.FileInformation;

/// <summary>
/// Handles file and directory name validation and generation within the <see cref="FileSurfer"/> app.
/// </summary>
public static class FileNameGenerator
{
    /// <summary>
    /// Finds a name available to use in <paramref name="directory"/> based on <paramref name="newName"/>.
    /// </summary>
    /// <returns><see cref="string"/> name available to use in <paramref name="directory"/>.</returns>
    public static string GetAvailableName(
        IFileInfoProvider fileInfoProvider,
        string directory,
        string newName
    )
    {
        IPathTools pathTools = fileInfoProvider.PathTools;
        if (!fileInfoProvider.PathExists(pathTools.Combine(directory, newName)))
            return newName;

        string nameWithoutExtension = Path.GetFileNameWithoutExtension(newName);
        string extension = Path.GetExtension(newName);
        for (int index = 1; ; index++)
        {
            string newFileName = $"{nameWithoutExtension} ({index}){extension}";

            if (!fileInfoProvider.PathExists(pathTools.Combine(directory, newFileName)))
                return newFileName;
        }
    }

    /// <summary>
    /// Finds a name available to use in all <paramref name="dirPaths"/> based on <paramref name="newName"/>.
    /// </summary>
    /// <returns><see cref="string"/> name available to use in all <paramref name="dirPaths"/>.</returns>
    public static string GetNameMultipleDirs(
        IFileInfoProvider fileInfoProvider,
        string newName,
        params string[] dirPaths
    )
    {
        IPathTools pathTools = fileInfoProvider.PathTools;
        if (
            dirPaths.All(dirPath =>
                !fileInfoProvider.PathExists(pathTools.Combine(dirPath, newName))
            )
        )
            return newName;

        string nameWithoutExtension = Path.GetFileNameWithoutExtension(newName);
        string extension = Path.GetExtension(newName);
        for (int index = 1; ; index++)
        {
            string newFileName = $"{nameWithoutExtension} ({index}){extension}";

            if (
                dirPaths.All(dirPath =>
                    !fileInfoProvider.PathExists(pathTools.Combine(dirPath, newFileName))
                )
            )
                return newFileName;
        }
    }

    /// <summary>
    /// Finds a name available for a copy.
    /// </summary>
    /// <returns>Name of a copy, available to use in the path specified in: <paramref name="directory"/>.</returns>
    public static string GetCopyName(
        IFileInfoProvider fileInfoProvider,
        string directory,
        IFileSystemEntry entry
    ) =>
        GetAvailableName(
            fileInfoProvider,
            directory,
            $"{entry.NameWoExtension} - Copy{entry.Extension}"
        );

    /// <summary>
    /// Determines if the files or directories represented by <paramref name="entries"/> can be collectively renamed.
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="entries"/> can be collectively renamed, otherwise <see langword="false"/>.</returns>
    public static bool CanBeRenamedCollectively(
        IList<IFileSystemEntry> entries,
        IPathTools pathTools
    )
    {
        if (entries.Count < 2)
            return true;

        bool onlyFiles = entries[0] is FileEntry;
        string extension = entries[0].Extension;

        for (int i = 1; i < entries.Count; i++)
            if (
                entries[i] is FileEntry != onlyFiles
                || pathTools.NamesAreEqual(entries[i].Extension, extension)
            )
                return false;

        return true;
    }

    /// <summary>
    /// Gets new available name for the files represented by <paramref name="entries"/> accoring to <paramref name="namingPattern"/>.
    /// </summary>
    /// <returns>An array of names available for <paramref name="entries"/>.</returns>
    public static string[] GetAvailableNames(
        IFileInfoProvider fileInfoProvider,
        IList<IFileSystemEntry> entries,
        string namingPattern
    )
    {
        IPathTools pathTools = fileInfoProvider.PathTools;

        if (entries.Count == 0)
            return Array.Empty<string>();

        string[] newNames = new string[entries.Count];
        string extension = Path.GetExtension(namingPattern);
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(namingPattern);
        string directory =
            pathTools.GetParentDir(entries.First().PathToEntry)
            ?? throw new ArgumentException(entries.First().PathToEntry);

        int lastIndex = 1;
        for (int i = 0; i < newNames.Length; i++)
        {
            for (int index = lastIndex; ; index++)
            {
                string newFileName = $"{nameWithoutExtension} ({index}){extension}";
                lastIndex++;

                if (!fileInfoProvider.PathExists(pathTools.Combine(directory, newFileName)))
                {
                    newNames[i] = newFileName;
                    break;
                }
            }
        }
        return newNames;
    }
}
