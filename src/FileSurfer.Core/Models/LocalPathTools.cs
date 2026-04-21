using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

namespace FileSurfer.Core.Models;

/// <summary>
/// Provides local file-system path operations using platform-specific separator and comparison rules.
/// </summary>
public class LocalPathTools : IPathTools
{
    /// <summary>
    /// Gets the singleton instance of <see cref="LocalPathTools"/>.
    /// </summary>
    public static readonly LocalPathTools Instance = new();

    char IPathTools.DirSeparator => DirSeparator;

    string IPathTools.NormalizePath(string path) => NormalizePath(path);

    string IPathTools.Combine(string pathBase, string pathSuffix) => Combine(pathBase, pathSuffix);

    string IPathTools.GetParentDir(string path) => GetParentDir(path);

    string IPathTools.GetFileName(string path) => GetFileName(path);

    string IPathTools.GetExtension(string path) => GetExtension(path);

    bool IPathTools.NamesAreEqual(string? nameA, string? nameB) => NamesAreEqual(nameA, nameB);

    bool IPathTools.PathsAreEqual(string? pathA, string? pathB) => PathsAreEqual(pathA, pathB);

    /// <summary>
    /// Gets the primary directory separator for the current platform.
    /// </summary>
    public static char DirSeparator => Path.DirectorySeparatorChar;

    /// <summary>
    /// Gets the alternate directory separator for the current platform.
    /// </summary>
    public static char OtherSeparator { get; } =
        OperatingSystem.IsWindows() ? Path.AltDirectorySeparatorChar : Path.DirectorySeparatorChar;

    /// <summary>
    /// Gets the string comparison mode used for path and name equality.
    /// </summary>
    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private LocalPathTools() { }

    /// <summary>
    /// Normalizes a local path by collapsing repeated separators and trimming trailing separators
    /// while preserving root paths.
    /// </summary>
    /// <param name="path">
    /// Path to normalize.
    /// </param>
    /// <returns>
    /// Normalized path.
    /// </returns>
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
            // GetFullPath may fail, continue without.
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

    /// <summary>
    /// Combines a base path and suffix into a single local path.
    /// </summary>
    /// <param name="pathBase">
    /// Base path.
    /// </param>
    /// <param name="pathSuffix">
    /// Path suffix to append.
    /// </param>
    /// <returns>
    /// Combined path with one separator between segments.
    /// </returns>
    public static string Combine(string pathBase, string pathSuffix) =>
        new StringBuilder(pathBase.Length + 1 + pathSuffix.Length)
            .Append(pathBase.AsSpan().TrimEnd(DirSeparator).TrimEnd(OtherSeparator))
            .Append(DirSeparator)
            .Append(pathSuffix.AsSpan().Trim(DirSeparator).Trim(OtherSeparator))
            .ToString();

    /// <summary>
    /// Gets the parent directory for the provided path.
    /// </summary>
    /// <param name="path">
    /// Path to inspect.
    /// </param>
    /// <returns>
    /// Parent directory path, or an empty string when no parent exists.
    /// </returns>
    public static string GetParentDir(string path)
    {
        ReadOnlySpan<char> trimmed = path.AsSpan().TrimEnd(DirSeparator).TrimEnd(OtherSeparator);
        ReadOnlySpan<char> parent = Path.GetDirectoryName(trimmed);
        return parent.ToString();
    }

    /// <summary>
    /// Gets the file or directory name segment from the provided path.
    /// </summary>
    /// <param name="path">
    /// Path to inspect.
    /// </param>
    /// <returns>
    /// Final segment name.
    /// </returns>
    public static string GetFileName(string path)
    {
        ReadOnlySpan<char> trimmed = path.AsSpan().TrimEnd(DirSeparator).TrimEnd(OtherSeparator);
        ReadOnlySpan<char> name = Path.GetFileName(trimmed);
        return name.ToString();
    }

    /// <summary>
    /// Gets the file extension (including leading dot) from the provided path.
    /// </summary>
    /// <param name="path">
    /// Path to inspect.
    /// </param>
    /// <returns>
    /// File extension when present; otherwise an empty string.
    /// </returns>
    public static string GetExtension(string path)
    {
        ReadOnlySpan<char> trimmed = path.AsSpan().TrimEnd(DirSeparator).TrimEnd(OtherSeparator);
        ReadOnlySpan<char> extension = Path.GetExtension(trimmed);
        return extension.ToString();
    }

    /// <summary>
    /// Determines whether two paths are equal after normalization.
    /// </summary>
    /// <param name="pathA">
    /// First path.
    /// </param>
    /// <param name="pathB">
    /// Second path.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when both paths are non-null and equal under local rules; otherwise <see langword="false"/>.
    /// </returns>
    public static bool PathsAreEqual(string? pathA, string? pathB) =>
        pathA is not null
        && pathB is not null
        && string.Equals(NormalizePath(pathA), NormalizePath(pathB), Comparison);

    /// <summary>
    /// Determines whether two already-normalized paths are equal.
    /// </summary>
    /// <param name="pathA">
    /// First normalized path.
    /// </param>
    /// <param name="pathB">
    /// Second normalized path.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when both paths are non-null and equal under local rules; otherwise <see langword="false"/>.
    /// </returns>
    public static bool PathsAreEqualNormalized(string? pathA, string? pathB) =>
        pathA is not null && pathB is not null && string.Equals(pathA, pathB, Comparison);

    /// <summary>
    /// Determines whether two file or directory names are equal under local rules.
    /// </summary>
    /// <param name="nameA">
    /// First name.
    /// </param>
    /// <param name="nameB">
    /// Second name.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when both names are non-null and equal; otherwise <see langword="false"/>.
    /// </returns>
    public static bool NamesAreEqual(string? nameA, string? nameB) =>
        nameA is not null && nameB is not null && string.Equals(nameA, nameB, Comparison);

    /// <summary>
    /// Enumerates all extension segments in a file name from left to right.
    /// </summary>
    /// <param name="path">
    /// Path whose file name extensions should be enumerated.
    /// </param>
    /// <returns>
    /// Sequence of extension parts without the leading dot.
    /// </returns>
    public static IEnumerable<string> EnumerateExtensions(string path)
    {
        int lastSep = int.Max(path.LastIndexOf(DirSeparator), path.LastIndexOf(OtherSeparator));

        for (int index = lastSep + 1; index <= path.Length - 2; index++)
            if (path[index] == '.')
                yield return path[(index + 1)..];
    }

    private static bool IsSep(char ch) => ch == DirSeparator || ch == OtherSeparator;

    private static void ShaveSep(StringBuilder sb)
    {
        for (int i = sb.Length - 1; i >= 0 && IsSep(sb[i]); i--)
            sb.Remove(sb.Length - 1, 1);
    }
}
