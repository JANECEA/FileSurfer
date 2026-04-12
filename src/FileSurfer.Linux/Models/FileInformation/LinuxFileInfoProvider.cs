using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.Shell;

namespace FileSurfer.Linux.Models.FileInformation;

/// <summary>
/// Optimizes icon delivery on Linux using the mime-type.
/// </summary>
public class LinuxFileInfoProvider : LocalFileInfoProvider
{
    private const string RootDir = "/";
    private readonly IShellHandler _shellHandler;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public LinuxFileInfoProvider(IShellHandler shellHandler) => _shellHandler = shellHandler;

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private sealed record LsblkEntry(string? Label, string? MountPoint, long Size, string? Type);

    private sealed record LsblkOutput(List<LsblkEntry> BlockDevices);

    public override DriveEntryInfo[] GetDrives()
    {
        DriveEntryInfo[] defaultList = [new(RootDir, "Root")];
        ValueResult<string> result = _shellHandler.ExecuteCommand(
            "lsblk",
            "-Jnbpo",
            "LABEL,MOUNTPOINT,SIZE,TYPE"
        );
        if (!result.IsOk || string.IsNullOrEmpty(result.Value))
            return defaultList;

        List<DriveEntryInfo> drives = new();
        LsblkOutput entries;
        try
        {
            entries =
                JsonSerializer.Deserialize<LsblkOutput>(result.Value, _jsonSerializerOptions)
                ?? throw new InvalidDataException();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return defaultList;
        }
        foreach (LsblkEntry entry in entries.BlockDevices)
            if (entry is { Type: "part", Label: not null, MountPoint: not null, Size: > 0 })
                drives.Add(new DriveEntryInfo(entry.MountPoint, entry.Label));

        return drives.Count > 0 ? drives.ToArray() : defaultList;
    }

    public override IEnumerable<string> GetSpecialFolders()
    {
        (Environment.SpecialFolder, string)[] folders =
        [
            (Environment.SpecialFolder.UserProfile, string.Empty),
            (Environment.SpecialFolder.UserProfile, "Downloads"),
            (Environment.SpecialFolder.MyDocuments, string.Empty),
            (Environment.SpecialFolder.MyPictures, string.Empty),
            (Environment.SpecialFolder.Desktop, string.Empty),
            (Environment.SpecialFolder.MyMusic, string.Empty),
            (Environment.SpecialFolder.MyVideos, string.Empty),
            (Environment.SpecialFolder.Templates, string.Empty),
        ];
        foreach ((Environment.SpecialFolder folder, string suffix) in folders)
        {
            string folderPath;
            try
            {
                folderPath = Path.Combine(Environment.GetFolderPath(folder), suffix);
            }
            catch
            {
                continue;
            }
            if (Directory.Exists(folderPath))
                yield return folderPath;
        }
    }

    public override bool IsHidden(string path, bool isDirectory)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        int i = path.Length - 1;
        while (i >= 0 && path[i] == LocalPathTools.DirSeparator)
            i--;

        for (; i >= 0; i--)
            if (path[i] == LocalPathTools.DirSeparator)
                break;

        return path[i + 1] == '.';
    }

    public override string GetRoot() => RootDir;

    public override bool IsLinkedToDirectory(string linkPath, out string directory)
    {
        directory = null!;
        FileInfo fileInfo = new(linkPath);
        if (
            fileInfo.LinkTarget is null
            || fileInfo.ResolveLinkTarget(true) is not { } linkTarget
            || !linkTarget.Attributes.HasFlag(FileAttributes.Directory)
        )
            return false;

        directory = linkTarget.FullName;
        return true;
    }
}
