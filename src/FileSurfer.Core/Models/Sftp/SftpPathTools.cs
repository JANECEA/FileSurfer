using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FileSurfer.Core.Models.Sftp;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal static class SftpPathTools
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

    private static void AssemblePath(StringBuilder sb, string path, char dirSep)
    {
        foreach (Range partRange in GetParts(path))
        {
            ReadOnlySpan<char> part = path.AsSpan()[partRange];
            sb.Append(part);
            sb.Append(dirSep);
        }
    }

    private static void RemoveTrailingSep(StringBuilder sb, char dirSep)
    {
        if (sb.Length > 0 && sb[^1] == dirSep)
            sb.Remove(sb.Length - 1, 1);
    }

    public static string Combine(string pathBase, string pathSuffix, char dirSep = '\0')
    {
        if (dirSep == '\0')
            dirSep = DirSeparator;

        StringBuilder sb = new(pathBase.Length + pathSuffix.Length);

        AssemblePath(sb, pathBase, dirSep);
        AssemblePath(sb, pathSuffix, dirSep);
        RemoveTrailingSep(sb, dirSep);

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
