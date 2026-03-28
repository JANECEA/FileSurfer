using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using ReactiveUI;

namespace FileSurfer.Core.ViewModels;

/// <summary>
/// Represents a local, displayable sidebar entry (file, directory, or drive) in the FileSurfer application.
/// </summary>
[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Properties are used by the window"
)]
public class SideBarEntryViewModel : ReactiveObject
{
    private const double HiddenOpacity = 0.5;

    /// <summary>
    /// Path to the file, directory, or drive represented by this <see cref="SideBarEntryViewModel"/>.
    /// </summary>
    public string PathToEntry { get; }

    /// <summary>
    /// Holds a <see cref="Bitmap"/> representing the file.
    /// </summary>
    public Bitmap? Icon
    {
        get => _icon;
        private set => this.RaiseAndSetIfChanged(ref _icon, value);
    }
    private Bitmap? _icon = null;

    /// <summary>=
    /// Holds the name of file, directory, or drive represented by this <see cref="SideBarEntryViewModel"/>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Holds this <see cref="SideBarEntryViewModel"/>'s displayed opacity
    /// </summary>
    public double Opacity { get; } = 1;

    /// <summary>
    /// Specifies if this <see cref="SideBarEntryViewModel"/> should be treated as a directory.
    /// </summary>
    public bool IsDirectory { get; }

    public SideBarEntryViewModel(IFileSystem fileSystem, IFileSystemEntry fileSystemEntry)
    {
        Name = fileSystemEntry.Name;
        PathToEntry = fileSystemEntry.PathToEntry;
        IsDirectory = fileSystemEntry is DirectoryEntry;

        if (fileSystem.FileInfoProvider.IsHidden(fileSystemEntry.PathToEntry, IsDirectory))
            Opacity = HiddenOpacity;

        _ = Task.Run(async () =>
            Icon = await LoadIconAsync(fileSystemEntry, fileSystem.IconProvider)
        );
    }

    private static Task<Bitmap> LoadIconAsync(IFileSystemEntry entry, IIconProvider iconProvider) =>
        entry switch
        {
            FileEntry => iconProvider.GetFileIcon(entry.PathToEntry),
            DirectoryEntry => iconProvider.GetDirectoryIcon(entry.PathToEntry),
            _ => throw new NotSupportedException(),
        };

    public SideBarEntryViewModel(IFileSystem fileSystem, DriveEntryInfo driveEntryInfo)
    {
        Name = driveEntryInfo.Name;
        PathToEntry = driveEntryInfo.PathToEntry;
        IsDirectory = true;

        _ = Task.Run(async () => Icon = await fileSystem.IconProvider.GetDriveIcon(driveEntryInfo));
    }
}
