using System.Collections.Generic;
using System.IO;

namespace FileSurfer.Linux.Models.FileInformation;

internal static class GlobsParser
{
    internal const string GlobsPath = "/usr/share/mime/globs";

    internal static Dictionary<string, string> Parse(StreamReader reader)
    {
        Dictionary<string, string> result = new();

        while (reader.ReadLine() is { } line)
            if (line.Length != 0 && line[0] != '#' && GetExtension(line) is { } extension)
                _ = result.TryAdd(extension, GetMimeType(line));

        return result;
    }

    private static string? GetExtension(string str)
    {
        int i = str.Length - 1;
        for (; i >= 0; i--)
            if (str[i] == '*')
                break;

        if (i < 0 || i >= str.Length - 3)
            return null;

        return str[(i + 2)..];
    }

    private static string GetMimeType(string str)
    {
        int i = str.Length - 1;
        for (; i >= 0; i--)
            if (str[i] == ':')
                break;

        return str[..i].Replace('/', '-');
    }
}
