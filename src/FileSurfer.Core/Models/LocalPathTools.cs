using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace FileSurfer.Core.Models;

/// <summary>
/// Provides methods for manipulating paths for specific filesystems
/// </summary>
public interface IPathTools
{
    /// <summary>
    /// Platform-specific character used to
    /// separate directory levels in a path string
    /// </summary>
    public char DirSeparator { get; }

    /// <summary>
    /// Normalizes the given path to a path without redundant separators and without a trailing separator
    /// <para/>
    /// Separators at root level paths are kept.
    /// </summary>
    /// <param name="path">Path to normalize</param>
    /// <returns>Normalized path</returns>
    public string NormalizePath(string path);

    /// <summary>
    /// Combines two paths
    /// Returns the combined path without trailing separators
    /// </summary>
    public string Combine(string pathBase, string pathSuffix);

    /// <summary>
    /// Returns the parent directory of the given path.
    /// Trailing separators are ignored
    /// Returns <see cref="string.Empty"/> if path has no parent directory
    /// </summary>
    public string GetParentDir(string path);

    /// <summary>
    /// Returns the name and extension parts of the given path.
    /// The resulting string contains the characters of path that follow the last separator in path.
    /// Trailing separators are ignored
    /// </summary>
    public string GetFileName(string path);

    /// <summary>
    /// Returns extension part of the given path.
    /// Trailing separators are ignored
    /// </summary>
    public string GetExtension(string path);

    /// <summary>
    /// Determines if two file or directory names are equal under the relevant filesystem's rules
    /// </summary>
    public bool NamesAreEqual(string? nameA, string? nameB);

    /// <summary>
    /// Determines if two file or directory paths are equal under the relevant filesystem's rules
    /// </summary>
    public bool PathsAreEqual(string? pathA, string? pathB);
}

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class LocalPathTools : IPathTools
{
    public static readonly LocalPathTools Instance = new();

    char IPathTools.DirSeparator => DirSeparator;

    string IPathTools.NormalizePath(string path) => NormalizePath(path);

    string IPathTools.Combine(string pathBase, string pathSuffix) => Combine(pathBase, pathSuffix);

    string IPathTools.GetParentDir(string path) => GetParentDir(path);

    string IPathTools.GetFileName(string path) => GetFileName(path);

    string IPathTools.GetExtension(string path) => GetExtension(path);

    bool IPathTools.NamesAreEqual(string? nameA, string? nameB) => NamesAreEqual(nameA, nameB);

    bool IPathTools.PathsAreEqual(string? pathA, string? pathB) => PathsAreEqual(pathA, pathB);

    public static char DirSeparator => Path.DirectorySeparatorChar;
    public static char OtherSeparator { get; } =
        OperatingSystem.IsWindows() ? Path.AltDirectorySeparatorChar : Path.DirectorySeparatorChar;
    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private LocalPathTools() { }

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
            // GetFullPath may fail, continue with this step
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
        ReadOnlySpan<char> trimmed = path.AsSpan().TrimEnd(DirSeparator).TrimEnd(OtherSeparator);
        ReadOnlySpan<char> parent = Path.GetDirectoryName(trimmed);
        return parent.IsEmpty ? path : parent.ToString();
    }

    public static string GetFileName(string path)
    {
        ReadOnlySpan<char> trimmed = path.AsSpan().TrimEnd(DirSeparator).TrimEnd(OtherSeparator);
        ReadOnlySpan<char> name = Path.GetFileName(trimmed);
        return name.ToString();
    }

    public static string GetExtension(string path)
    {
        ReadOnlySpan<char> trimmed = path.AsSpan().TrimEnd(DirSeparator).TrimEnd(OtherSeparator);
        ReadOnlySpan<char> extension = Path.GetExtension(trimmed);
        return extension.ToString();
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

    private static StringBuilder ShaveSep(StringBuilder sb)
    {
        for (int i = sb.Length - 1; i >= 0 && IsSep(sb[i]); i--)
            sb.Remove(sb.Length - 1, 1);

        return sb;
    }

    public static string TrimEndDirectorySeparator(string path) =>
        ShaveSep(new StringBuilder(path)).ToString();
}
