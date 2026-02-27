using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace FileSurfer.Core.Models;

public interface IPathTools
{
    public char DirSeparator { get; }

    /// <summary>
    /// Normalizes the given path to a path without redundant separators and without a trailing separator
    /// <para/>
    /// Separators at root level paths are kept.
    /// </summary>
    /// <param name="path">Path to normalize</param>
    /// <returns>Normalized path</returns>
    public string NormalizePath(string path);

    public string Combine(string pathBase, string pathSuffix);

    public string GetParentDir(string path);

    public string GetFileName(string path);
}

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class LocalPathTools : IPathTools
{
    char IPathTools.DirSeparator => DirSeparator;

    string IPathTools.NormalizePath(string path) => NormalizePath(path);

    string IPathTools.Combine(string pathBase, string pathSuffix) => Combine(pathBase, pathSuffix);

    string IPathTools.GetParentDir(string path) => GetParentDir(path);

    string IPathTools.GetFileName(string path) => GetFileName(path);

    public static char DirSeparator => Path.DirectorySeparatorChar;
    public static char OtherSeparator { get; } =
        OperatingSystem.IsWindows() ? Path.AltDirectorySeparatorChar : Path.DirectorySeparatorChar;
    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

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
            // GetFullPath may fail, continue with normalizing
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

    public static string Combine(string pathBase, string pathSuffix) =>
        new StringBuilder(pathBase.Length + 1 + pathSuffix.Length)
            .Append(pathBase.AsSpan().TrimEnd(DirSeparator).TrimEnd(OtherSeparator))
            .Append(DirSeparator)
            .Append(pathSuffix.AsSpan().Trim(DirSeparator).Trim(OtherSeparator))
            .ToString();

    public static string GetParentDir(string path)
    {
        ReadOnlySpan<char> shaved = path.AsSpan().TrimEnd(DirSeparator).TrimEnd(OtherSeparator);
        ReadOnlySpan<char> parent = Path.GetDirectoryName(shaved);
        return parent.IsEmpty ? path : parent.ToString();
    }

    public static string GetFileName(string path)
    {
        ReadOnlySpan<char> shaved = path.AsSpan().TrimEnd(DirSeparator).TrimEnd(OtherSeparator);
        ReadOnlySpan<char> name = Path.GetFileName(shaved);
        return name.ToString();
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
        int lastSep = int.Max(path.LastIndexOf(DirSeparator), path.LastIndexOf(OtherSeparator));

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

    public static string TrimEndDirectorySeparator(string path) =>
        path.AsSpan().TrimEnd(DirSeparator).TrimEnd(OtherSeparator).ToString();
}
