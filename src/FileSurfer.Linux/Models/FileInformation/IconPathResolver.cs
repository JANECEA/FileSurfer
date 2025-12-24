using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FileSurfer.Linux.Models.FileInformation;

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

    internal static List<string> GetSearchPaths()
    {
        string? theme = GetCurrentTheme();
        List<string> result = new();

        if (theme is not null)
            SearchPaths(result, theme);

        if (result.Count == 0)
            SearchPaths(result, null);

        return result.Distinct().ToList();
    }

    private static void SearchPaths(List<string> result, string? theme)
    {
        foreach (string baseIconDir in BaseIconDirs)
        foreach (string extension in SupportedExtensions)
        foreach (
            string iconPath in Directory.EnumerateFiles(
                baseIconDir,
                $"{RequiredIcon}.{extension}",
                SearchOption.AllDirectories
            )
        )
            if (ValidatePath(iconPath, baseIconDir, theme))
                result.Add(Path.GetDirectoryName(iconPath)!);
    }

    private static bool ValidatePath(string iconPath, string baseIconDir, string? theme)
    {
        string restOfPath = iconPath[baseIconDir.Length..];
        if (restOfPath.Contains('@') || (theme is not null && !restOfPath.Contains(theme)))
            return false;

        return GetSize(restOfPath) >= PreferredSize;
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

    private static string? GetCurrentTheme()
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = "gsettings",
                Arguments = "get org.gnome.desktop.interface icon-theme",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process? process = Process.Start(psi);
            if (process == null)
                return null;

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return null;

            return output.Trim('\'');
        }
        catch
        {
            return null;
        }
    }
}
