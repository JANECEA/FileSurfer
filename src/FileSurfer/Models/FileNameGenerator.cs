using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileSurfer;

static class FileNameGenerator
{
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

    public static string GetCopyName(string directory, FileSystemEntry entry)
    {
        string extension = entry.IsDirectory ? string.Empty : Path.GetExtension(entry.PathToEntry);
        string copyName = entry.Name;
        if (extension != string.Empty)
            copyName = Path.GetFileNameWithoutExtension(entry.PathToEntry);

        return GetAvailableName(directory, copyName + " - Copy" + extension);
    }

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
                || (onlyFiles && Path.GetExtension(entry.PathToEntry) != extension)
            )
                return false;
        }
        return true;
    }

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
