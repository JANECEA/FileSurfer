using System.IO;

namespace FileSurfer;

static class FileNameGenerator
{
    public static string GetAvailableName(string path, string fileName)
    {
        if (!Path.Exists(Path.Combine(path, fileName)))
        {
            return fileName;
        }
        string nameWOextension = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        for (int index = 1; ; index++)
        {
            string newFileName = $"{nameWOextension} ({index}){extension}";
            if (!Path.Exists(Path.Combine(path, newFileName)))
            {
                return newFileName;
            }
        }
    }

    public static string GetCopyName(string path, FileSystemEntry entry)
    {
        string extension = entry.IsDirectory ? string.Empty : Path.GetExtension(entry.PathToEntry);
        string copyName = entry.Name;
        if (extension != string.Empty)
            copyName = Path.GetFileNameWithoutExtension(entry.PathToEntry);

        return GetAvailableName(path, copyName + " - Copy" + extension);
    }
}
