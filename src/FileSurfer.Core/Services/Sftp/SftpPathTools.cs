using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FileSurfer.Core.Services.Sftp;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal static class SftpPathTools
{
    public const string RootDir = "/";
    public const char DirSeparator = '/';

    private static int LastNonSep(string path, int startIndex = -1)
    {
        int i = path.Length - 1;
        if (startIndex >= 0)
            i = startIndex;

        for (; 0 <= i && i < path.Length; i--)
            if (path[i] != DirSeparator)
                return i;

        return -1;
    }

    internal static string Combine(string pathBase, string name)
    {
        int lastNonSep = LastNonSep(pathBase);
        if (lastNonSep < 0)
            return DirSeparator + name;

        StringBuilder sb = new(pathBase.Length + name.Length + 1);
        for (int j = 0; j <= lastNonSep; j++)
            sb.Append(pathBase[j]);

        sb.Append(DirSeparator);
        sb.Append(name);
        return sb.ToString();
    }

    private static IEnumerable<Range> GetParts(string path)
    {
        int exclusivePartEnd = path.Length;
        for (int i = path.Length - 1; i >= 0; i--)
        {
            if (path[i] != DirSeparator)
                continue;

            if (exclusivePartEnd - i - 1 > 0)
                yield return new Range(i + 1, exclusivePartEnd);

            exclusivePartEnd = i;
        }
    }

    internal static string GetFileName(string path)
    {
        using IEnumerator<Range> enumerator = GetParts(path).GetEnumerator();
        return !enumerator.MoveNext() ? string.Empty : path[enumerator.Current];
    }

    internal static string GetParentDir(string path)
    {
        using IEnumerator<Range> enumerator = GetParts(path).GetEnumerator();
        if (!enumerator.MoveNext() || !enumerator.MoveNext())
            return string.Empty;

        return path[..enumerator.Current.End];
    }
}
