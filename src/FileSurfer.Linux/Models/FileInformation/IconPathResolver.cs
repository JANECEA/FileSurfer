using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Shell;

namespace FileSurfer.Linux.Models.FileInformation;

internal sealed class IconPathComparer : IComparer<IconPath>
{
    private readonly string? _theme;

    internal IconPathComparer(string? theme) => _theme = theme;

    public int Compare(IconPath? a, IconPath? b)
    {
        if (ReferenceEquals(a, b))
            return 0;

        if (a is null || b is null)
            return a is null ? -1 : 1;

        bool aIsTheme =
            _theme is not null
            && a.RestOfPath.Contains($"{PathTools.DirSeparator}{_theme}{PathTools.DirSeparator}");
        bool bIsTheme =
            _theme is not null
            && b.RestOfPath.Contains($"{PathTools.DirSeparator}{_theme}{PathTools.DirSeparator}");
        if (aIsTheme != bIsTheme)
            return aIsTheme ? -1 : 1;

        bool aContainsTheme = _theme is not null && a.RestOfPath.Contains(_theme);
        bool bContainsTheme = _theme is not null && b.RestOfPath.Contains(_theme);
        if (aContainsTheme != bContainsTheme)
            return aContainsTheme ? -1 : 1;

        if (a.IconCount != b.IconCount)
            return a.IconCount > b.IconCount ? -1 : 1;

        bool aIsLocal = a.BaseDir.Contains($"{PathTools.DirSeparator}home{PathTools.DirSeparator}");
        bool bIsLocal = b.BaseDir.Contains($"{PathTools.DirSeparator}home{PathTools.DirSeparator}");
        if (aIsLocal != bIsLocal)
            return aIsLocal ? -1 : 1;

        if (a.Size != b.Size)
            return a.Size < b.Size ? -1 : 1;

        return 0;
    }
}

internal sealed record IconPath(
    int Size,
    int IconCount,
    string RestOfPath,
    string BaseDir,
    string PathToIcons
);

internal static class IconPathResolver
{
    private const string RequiredIcon = "text-plain";
    private const int PreferredSize = 64;
    private static readonly IReadOnlyList<string> BaseIconDirs = GetBaseIconDirs();
    private static readonly IReadOnlyList<string> SupportedExtensions = ["svg", "png"];

    private static List<string> GetBaseIconDirs()
    {
        string[] baseIconDirs =
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "icons/"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".icons/"
            ),
            "/usr/share/icons/",
        };
        return baseIconDirs.Where(Directory.Exists).ToList();
    }

    private static string? GetCurrentTheme(IShellHandler shellHandler)
    {
        ValueResult<string> result = shellHandler.ExecuteCommand(
            "gsettings",
            "get org.gnome.desktop.interface icon-theme"
        );
        if (!result.IsOk || string.IsNullOrEmpty(result.Value))
            return null;

        return result.Value.Trim('\'');
    }

    internal static List<string> GetSearchPaths(IShellHandler shellHandler)
    {
        string? theme = GetCurrentTheme(shellHandler);
        List<IconPath> result = SearchPaths();

        result.Sort(new IconPathComparer(theme));
        return result.Select(iconPath => iconPath.PathToIcons).Distinct().ToList();
    }

    private static List<IconPath> SearchPaths()
    {
        List<IconPath> result = new();
        foreach (string baseIconDir in BaseIconDirs)
        foreach (string extension in SupportedExtensions)
        foreach (
            string path in Directory.EnumerateFiles(
                baseIconDir,
                $"{RequiredIcon}.{extension}",
                SearchOption.AllDirectories
            )
        )
            if (TryGetIconPath(path, baseIconDir, out IconPath? iconPath))
                result.Add(iconPath!);

        return result;
    }

    private static bool TryGetIconPath(string path, string baseIconDir, out IconPath? iconPath)
    {
        iconPath = null;
        string restOfPath = path[baseIconDir.Length..];
        if (restOfPath.Contains('@'))
            return false;

        int size = GetSize(restOfPath);
        if (size < PreferredSize)
            return false;

        string? pathToIcons = Path.GetDirectoryName(path);
        if (pathToIcons is null)
            return false;

        int iconCount = Directory.EnumerateFiles(pathToIcons).Count();
        iconPath = new IconPath(
            size,
            iconCount,
            path[baseIconDir.Length..],
            baseIconDir,
            pathToIcons
        );
        return true;
    }

    private static int GetSize(string restOfPath)
    {
        while (Path.GetDirectoryName(restOfPath) is string dirName)
        {
            string fileName = Path.GetFileName(dirName);
            if (string.Equals(fileName, "scalable", StringComparison.OrdinalIgnoreCase))
                return int.MaxValue;

            if (uint.TryParse(fileName, out uint size))
                return (int)size;

            int index = fileName.IndexOf('x');
            if (
                index != -1
                && uint.TryParse(fileName[..index], out uint width)
                && uint.TryParse(fileName[(index + 1)..], out uint height)
            )
                return (int)Math.Min(width, height);

            restOfPath = dirName;
        }
        return -1;
    }
}
