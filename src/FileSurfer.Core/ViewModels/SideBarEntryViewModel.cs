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
    public string PathToEntry => FileSystemEntry.PathToEntry;

    /// <summary>
    /// Specifies if this <see cref="SideBarEntryViewModel"/> is a directory.
    /// </summary>
    public bool IsDirectory => FileSystemEntry is DirectoryEntry or DriveEntry;

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
    public string Name => FileSystemEntry.Name;

    /// <summary>
    /// Holds this <see cref="SideBarEntryViewModel"/>'s displayed opacity
    /// </summary>
    public double Opacity { get; }

    /// <summary>
    /// Holds the underlying <see cref="IFileSystemEntry"/>.
    /// </summary>
    public IFileSystemEntry FileSystemEntry { get; }

    public SideBarEntryViewModel(IFileSystem fileSystem, IFileSystemEntry fileSystemEntry)
    {
        FileSystemEntry = fileSystemEntry;
        Opacity = fileSystem.FileInfoProvider.IsHidden(fileSystemEntry.PathToEntry, IsDirectory)
            ? HiddenOpacity
            : 1;

        _ = LoadIconAsync(fileSystemEntry, fileSystem.IconProvider);
    }

    private async Task LoadIconAsync(IFileSystemEntry entry, IIconProvider iconProvider) =>
        Icon = entry switch
        {
            FileEntry => await iconProvider.GetFileIcon(entry.PathToEntry),
            DirectoryEntry => await iconProvider.GetDirectoryIcon(entry.PathToEntry),
            DriveEntry driveEntry => await iconProvider.GetDriveIcon(driveEntry),
            _ => throw new NotSupportedException(),
        };
}
