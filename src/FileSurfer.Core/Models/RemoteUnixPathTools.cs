using System;
using System.Text;

// ReSharper disable MemberCanBePrivate.Global

namespace FileSurfer.Core.Models;

/// <summary>
/// Provides Unix-style path operations for remote paths using forward slash separators.
/// </summary>
internal class RemoteUnixPathTools : IPathTools
{
    /// <summary>
    /// Gets the singleton instance of <see cref="RemoteUnixPathTools"/>.
    /// </summary>
    public static readonly RemoteUnixPathTools Instance = new();

    char IPathTools.DirSeparator => DirSeparator;

    string IPathTools.NormalizePath(string path) => NormalizePath(path);

    string IPathTools.Combine(string pathBase, string pathSuffix) => Combine(pathBase, pathSuffix);

    string IPathTools.GetParentDir(string path) => GetParentDir(path);

    string IPathTools.GetFileName(string path) => GetFileName(path);

    string IPathTools.GetExtension(string path) => GetExtension(path);

    bool IPathTools.NamesAreEqual(string? nameA, string? nameB) => NamesAreEqual(nameA, nameB);

    bool IPathTools.PathsAreEqual(string? pathA, string? pathB) => PathsAreEqual(pathA, pathB);

    /// <summary>
    /// Directory separator used by this path tool.
    /// </summary>
    public const char DirSeparator = '/';

    /// <summary>
    /// Root directory path.
    /// </summary>
    public const string RootDir = "/";

    private RemoteUnixPathTools() { }

    /// <summary>
    /// Normalizes a path by ensuring a leading separator, collapsing repeated separators,
    /// and trimming trailing separators except for the root path.
    /// </summary>
    /// <param name="path">
    /// Path to normalize.
    /// </param>
    /// <returns>
    /// Normalized Unix-style path.
    /// </returns>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        StringBuilder sb = new(path.Length);
        if (path[0] != DirSeparator)
            sb.Append(DirSeparator);

        bool previousWasDirSep = false;
        foreach (char ch in path)
        {
            if (ch != DirSeparator)
                sb.Append(ch);
            else if (!previousWasDirSep)
                sb.Append(DirSeparator);

            previousWasDirSep = ch == DirSeparator;
        }
        if (sb.Length > 1)
            ShaveSep(sb);

        return sb.ToString();
    }

    /// <summary>
    /// Combines a base path and suffix into a single Unix-style path.
    /// </summary>
    /// <param name="pathBase">
    /// Base path.
    /// </param>
    /// <param name="pathSuffix">
    /// Suffix path to append.
    /// </param>
    /// <returns>
    /// Combined path with one directory separator between segments.
    /// </returns>
    public static string Combine(string pathBase, string pathSuffix) =>
        new StringBuilder(pathBase.Length + 1 + pathSuffix.Length)
            .Append(pathBase.AsSpan().TrimEnd(DirSeparator))
            .Append(DirSeparator)
            .Append(pathSuffix.AsSpan().Trim(DirSeparator))
            .ToString();

    /// <summary>
    /// Determines whether two paths are equal after normalization using ordinal comparison.
    /// </summary>
    /// <param name="pathA">
    /// First path.
    /// </param>
    /// <param name="pathB">
    /// Second path.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when both paths are non-null and represent the same normalized path; otherwise <see langword="false"/>.
    /// </returns>
    public static bool PathsAreEqual(string? pathA, string? pathB) =>
        pathA is not null
        && pathB is not null
        && string.Equals(NormalizePath(pathA), NormalizePath(pathB), StringComparison.Ordinal);

    /// <summary>
    /// Determines whether two names are equal using ordinal comparison.
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
        nameA is not null
        && nameB is not null
        && string.Equals(nameA, nameB, StringComparison.Ordinal);

    private static void ShaveSep(StringBuilder sb)
    {
        for (int i = sb.Length - 1; i >= 0 && sb[i] == DirSeparator; i--)
            sb.Remove(sb.Length - 1, 1);
    }

    private static void ShaveNonSep(StringBuilder sb)
    {
        for (int i = sb.Length - 1; i >= 0 && sb[i] != DirSeparator; i--)
            sb.Remove(sb.Length - 1, 1);
    }

    /// <summary>
    /// Gets the parent directory for the provided path.
    /// </summary>
    /// <param name="path">
    /// Path to inspect.
    /// </param>
    /// <returns>
    /// Parent directory path, <see cref="RootDir"/>, or an empty string for empty input.
    /// </returns>
    public static string GetParentDir(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        StringBuilder sb = new(path);

        ShaveSep(sb);
        if (sb.Length == 0)
            return string.Empty;

        ShaveNonSep(sb);
        ShaveSep(sb);

        return sb.Length > 0 ? sb.ToString() : RootDir;
    }

    /// <summary>
    /// Gets the file or directory name segment from the provided path.
    /// </summary>
    /// <param name="path">
    /// Path to inspect.
    /// </param>
    /// <returns>
    /// Final segment name, or an empty string for empty input.
    /// </returns>
    public static string GetFileName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        ReadOnlySpan<char> trimmed = path.AsSpan().TrimEnd(DirSeparator);
        int sepIndex = trimmed.LastIndexOf(DirSeparator);
        if (sepIndex == -1)
            return trimmed.ToString();

        return trimmed[(sepIndex + 1)..].ToString();
    }

    /// <summary>
    /// Gets the file extension (including leading dot) from the provided path.
    /// </summary>
    /// <param name="path">
    /// Path to inspect.
    /// </param>
    /// <returns>
    /// File extension when present after the last separator; otherwise an empty string.
    /// </returns>
    public static string GetExtension(string path)
    {
        ReadOnlySpan<char> trimmed = path.AsSpan().TrimEnd(DirSeparator);
        int sepIndex = trimmed.LastIndexOf(DirSeparator);
        int dotIndex = trimmed.LastIndexOf('.');

        return sepIndex < dotIndex ? trimmed[dotIndex..].ToString() : string.Empty;
    }
}
