using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FileSurfer.Core.Models.Sftp;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal static class RemoteUnixPathTools // TODO simplify
{
    public const char DirSeparator = '/';
    public const string RootDir = "/";

    private static IEnumerable<Range> GetParts(string path)
    {
        if (path.Length == 0)
            yield break;

        int inclusiveStart = 0;
        if (path[0] == DirSeparator)
        {
            inclusiveStart = 1;
            yield return new Range(0, 0);
        }

        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] != DirSeparator)
                continue;

            if (inclusiveStart < i - 1)
                yield return new Range(inclusiveStart, i);

            inclusiveStart = i;
        }

        if (inclusiveStart < path.Length - 1)
            yield return new Range(inclusiveStart, path.Length);
    }

    private static IEnumerable<Range> GetPartsReversed(string path)
    {
        if (path.Length == 0)
            yield break;

        int exclusiveEnd = path.Length;
        for (int i = path.Length - 1; i >= 0; i--)
        {
            if (path[i] != DirSeparator)
                continue;

            if (exclusiveEnd > i + 1)
                yield return new Range(i + 1, exclusiveEnd);

            exclusiveEnd = i;
        }

        if (path[0] == DirSeparator)
            yield return new Range(0, 0);
    }

    private static void AssemblePath(StringBuilder sb, string path)
    {
        foreach (Range partRange in GetParts(path))
        {
            ReadOnlySpan<char> part = path.AsSpan()[partRange];
            sb.Append(part);
            //sb.Append(DirSeparator);
        }
    }

    /// <summary>
    /// Normalizes the given path to and removes redundant separators and the trailing separator
    /// <para/>
    /// Separators at root level paths are kept.
    /// </summary>
    /// <param name="path">Path to normalize</param>
    /// <returns>Normalized path</returns>
    public static string NormalizePath(string path)
    {
        StringBuilder sb = new(path.Length);
        AssemblePath(sb, path);
        RemoveTrailingSep(sb);

        return sb.ToString();
    }

    private static void RemoveTrailingSep(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[^1] == DirSeparator)
            sb.Remove(sb.Length - 1, 1);
    }

    public static string Combine(string pathBase, string pathSuffix)
    {
        StringBuilder sb = new(pathBase.Length + pathSuffix.Length);

        AssemblePath(sb, pathBase);
        AssemblePath(sb, pathSuffix);
        RemoveTrailingSep(sb);

        return sb.ToString();
    }

    internal static string GetFileName(string path)
    {
        using IEnumerator<Range> enumerator = GetPartsReversed(path).GetEnumerator();
        return !enumerator.MoveNext() ? string.Empty : path[enumerator.Current];
    }

    internal static string GetParentDir(string path)
    {
        using IEnumerator<Range> enumerator = GetPartsReversed(path).GetEnumerator();
        if (!enumerator.MoveNext() || !enumerator.MoveNext())
            return string.Empty;

        return path[..enumerator.Current.End];
    }
}
