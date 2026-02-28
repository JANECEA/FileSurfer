using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FileSurfer.Core.Models.Sftp;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal class RemoteUnixPathTools : IPathTools
{
    char IPathTools.DirSeparator => DirSeparator;

    string IPathTools.NormalizePath(string path) => NormalizePath(path);

    string IPathTools.Combine(string pathBase, string pathSuffix) => Combine(pathBase, pathSuffix);

    string IPathTools.GetParentDir(string path) => GetParentDir(path);

    string IPathTools.GetFileName(string path) => GetFileName(path);

    bool IPathTools.NamesAreEqual(string? nameA, string? nameB) => NamesAreEqual(nameA, nameB);

    bool IPathTools.PathsAreEqual(string? pathA, string? pathB) => PathsAreEqual(pathA, pathB);

    public const char DirSeparator = '/';
    public const string RootDir = "/";

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

    public static bool PathsAreEqual(string? pathA, string? pathB) =>
        pathA is not null
        && pathB is not null
        && string.Equals(NormalizePath(pathA), NormalizePath(pathB), StringComparison.Ordinal);

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

    public static string GetParentDir(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        StringBuilder sb = new(path);

        ShaveSep(sb);
        ShaveNonSep(sb);
        ShaveSep(sb);

        return sb.Length > 0 ? sb.ToString() : RootDir;
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
