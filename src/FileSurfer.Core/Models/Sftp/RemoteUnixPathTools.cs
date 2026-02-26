using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FileSurfer.Core.Models.Sftp;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal static class RemoteUnixPathTools // TODO simplify
{
    public const char DirSeparator = '/';
    public const string RootDir = "/";

    /// <summary>
    /// Normalizes the given path to and removes redundant separators and the trailing separator
    /// <para/>
    /// Separators at root level paths are kept.
    /// </summary>
    /// <param name="path">Path to normalize</param>
    /// <returns>Normalized path</returns>
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

    public static string Combine(string pathBase, string pathSuffix) =>
        new StringBuilder(pathBase.Length + 1 + pathSuffix.Length)
            .Append(pathBase.AsSpan().TrimEnd(DirSeparator))
            .Append(DirSeparator)
            .Append(pathSuffix.AsSpan().Trim(DirSeparator))
            .ToString();

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
        while (nameEndExc >= 0 && path[nameEndExc] == DirSeparator)
            nameEndExc--;

        int nameStartInc = nameEndExc;
        while (nameStartInc >= 0 && path[nameStartInc] != DirSeparator)
            nameStartInc--;

        return nameStartInc >= 0 && nameEndExc >= 0 && nameStartInc < nameEndExc
            ? path[nameStartInc..nameEndExc]
            : string.Empty;
    }
}
