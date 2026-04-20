using System.Collections.Generic;
using System.IO;

namespace FileSurfer.Linux.Models.FileInformation;

/// <summary>
/// Parses Linux MIME glob mappings from the shared globs database file.
/// </summary>
internal static class GlobsParser
{
    /// <summary>
    /// Gets the default path to the system MIME globs file.
    /// </summary>
    internal const string GlobsPath = "/usr/share/mime/globs";

    /// <summary>
    /// Reads MIME glob lines and returns extension-to-MIME mappings.
    /// </summary>
    internal static Dictionary<string, string> Parse(StreamReader reader)
    {
        Dictionary<string, string> result = new();

        while (reader.ReadLine() is { } line)
            if (
                line.Length != 0
                && line[0] != '#'
                && GetExtension(line) is { } extension
                && GetMimeType(line) is { } mimeType
            )
                _ = result.TryAdd(extension, mimeType);

        return result;
    }

    private static string? GetExtension(string str)
    {
        int i = str.Length - 1;
        for (; i >= 0; i--)
            if (str[i] == '*')
                break;

        if (0 < i && i <= str.Length - 3)
            return str[(i + 2)..];

        return null;
    }

    private static string? GetMimeType(string str)
    {
        int i = str.Length - 1;
        for (; i >= 0; i--)
            if (str[i] == ':')
                break;

        return i <= 0 ? null : str[..i].Replace('/', '-');
    }
}
