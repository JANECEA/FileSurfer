using System;
using System.IO;
using System.Text;

namespace FileSurfer.Core.Models;

public static class PathTools
{
    public static char DirSeparator { get; } = Path.DirectorySeparatorChar;
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
    internal static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        path = Path.GetFullPath(path);
        StringBuilder sb = new();

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

    internal static bool PathsAreEqual(string? pathA, string? pathB) =>
        pathA is not null
        && pathB is not null
        && string.Equals(NormalizePath(pathA), NormalizePath(pathB), Comparison);

    internal static bool NamesAreEqual(string? nameA, string? nameB) =>
        nameA is not null && nameB is not null && string.Equals(nameA, nameB, Comparison);
}
