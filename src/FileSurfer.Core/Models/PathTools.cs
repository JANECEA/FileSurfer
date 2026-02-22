using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace FileSurfer.Core.Models;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class PathTools
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
    public static string NormalizeLocalPath(string path) => NormalizePathInternal(path, true);

    /// <summary>
    /// Normalizes the given path to and removes redundant separators and the trailing separator
    /// <para/>
    /// Separators at root level paths are kept.
    /// </summary>
    /// <param name="path">Path to normalize</param>
    /// <returns>Normalized path</returns>
    public static string NormalizePath(string path) => NormalizePathInternal(path, false);

    private static string NormalizePathInternal(string path, bool getFullPath)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (getFullPath)
            path = Path.GetFullPath(path);

        StringBuilder sb = new(path.Length);

        bool previousWasDirSep = false;
        foreach (char ch in path)
        {
            bool isSep = ch == DirSeparator || ch == OtherSeparator;
            if (!isSep)
                sb.Append(ch);
            else if (!previousWasDirSep)
                sb.Append(DirSeparator);

            previousWasDirSep = isSep;
        }
        string root = Path.GetPathRoot(path) ?? string.Empty;
        if (sb.Length != 1 && sb.Length != root.Length && sb[^1] == DirSeparator)
            sb.Remove(sb.Length - 1, 1);

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
}
