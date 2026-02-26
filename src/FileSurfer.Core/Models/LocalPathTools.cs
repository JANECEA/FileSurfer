using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace FileSurfer.Core.Models;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class LocalPathTools
{
    public static char DirSeparator => Path.DirectorySeparatorChar;
    public static char OtherSeparator { get; } = OperatingSystem.IsWindows() ? '/' : DirSeparator;
    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Normalizes the given path to an absolute path without redundant separators and without a trailing separator
    /// <para/>
    /// Separators at root level paths are kept.
    /// </summary>
    /// <param name="path">Path to normalize</param>
    /// <returns>Normalized path</returns>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        try
        {
            path = Path.GetFullPath(path);
        }
        catch
        {
            // Continue normalization
        }
        StringBuilder sb = new(path.Length);

        bool previousWasDirSep = false;
        foreach (char ch in path)
        {
            if (!IsSep(ch))
                sb.Append(ch);
            else if (!previousWasDirSep)
                sb.Append(DirSeparator);

            previousWasDirSep = IsSep(ch);
        }
        string root = Path.GetPathRoot(path) ?? string.Empty;
        if (sb.Length != root.Length)
            ShaveSep(sb);

        return sb.ToString();
    }

    public static bool PathsAreEqual(string? pathA, string? pathB) =>
        pathA is not null
        && pathB is not null
        && string.Equals(NormalizePath(pathA), NormalizePath(pathB), Comparison);

    public static bool PathsAreEqualNormalized(string? pathA, string? pathB) =>
        pathA is not null && pathB is not null && string.Equals(pathA, pathB, Comparison);

    public static bool NamesAreEqual(string? nameA, string? nameB) =>
        nameA is not null && nameB is not null && string.Equals(nameA, nameB, Comparison);

    public static IEnumerable<string> EnumerateExtensions(string path)
    {
        int lastSep = path.LastIndexOf(DirSeparator);

        for (int index = lastSep + 1; index <= path.Length - 3; index++)
            if (path[index] == '.')
                yield return path[(index + 1)..];
    }

    private static bool IsSep(char ch) => ch == DirSeparator || ch == OtherSeparator;

    private static void ShaveSep(StringBuilder sb)
    {
        for (int i = sb.Length - 1; i >= 0 && IsSep(sb[i]); i--)
            sb.Remove(sb.Length - 1, 1);
    }

    private static void ShaveNonSep(StringBuilder sb)
    {
        for (int i = sb.Length - 1; i >= 0 && !IsSep(sb[i]); i--)
            sb.Remove(sb.Length - 1, 1);
    }

    public static string GetParentDir(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        StringBuilder sb = new(path);

        ShaveSep(sb);
        ShaveNonSep(sb);
        ShaveSep(sb);

        return sb.ToString();
    }

    public static string GetFileName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        int nameEndExc = path.Length - 1;
        while (nameEndExc >= 0 && IsSep(path[nameEndExc]))
            nameEndExc--;

        int nameStartInc = nameEndExc;
        while (nameStartInc >= 0 && !IsSep(path[nameStartInc]))
            nameStartInc--;

        return nameStartInc >= 0 && nameEndExc >= 0 && nameStartInc < nameEndExc
            ? path[nameStartInc..nameEndExc]
            : string.Empty;
    }
}
