using System;
using Avalonia.Controls;
using FileSurfer.Core.ViewModels;
using FileSurfer.Linux.Views;

namespace FileSurfer.Linux.ViewModels;

/// <summary>
/// Represents an object that can be displayed
/// </summary>
public interface IDisplayable
{
    /// <summary>
    /// Display the object
    /// </summary>
    public void Show();
}

/// <summary>
/// The PropertiesWindowViewModel is the ViewModel for the <see cref="PropertiesWindow"/>.
/// </summary>
public sealed class PropertiesWindowViewModel : IDisplayable
{
    private readonly Window _mainWindow;

    public FileSystemEntryViewModel EntryVm { get; }
    public required string Size { get; init; }
    public required string DateCreated { get; init; }
    public required string DateAccessed { get; init; }
    public required string DateModified { get; init; }
    public required string Owner { get; init; }
    public string PermissionsOwner { get; }
    public string PermissionsGroup { get; }
    public string PermissionsOther { get; }

    public PropertiesWindowViewModel(
        FileSystemEntryViewModel entry,
        Window mainWindow,
        string permissions
    )
    {
        EntryVm = entry;
        _mainWindow = mainWindow;

        ReadOnlySpan<char> perms = permissions.AsSpan();
        if (entry.IsDirectory)
        {
            PermissionsOwner = GetDirPerms(perms[0..3]);
            PermissionsGroup = GetDirPerms(perms[3..6]);
            PermissionsOther = GetDirPerms(perms[6..9]);
        }
        else
        {
            PermissionsOwner = GetFilePerms(perms[0..3]);
            PermissionsGroup = GetFilePerms(perms[3..6]);
            PermissionsOther = GetFilePerms(perms[6..9]);
        }
    }

    private static string GetFilePerms(ReadOnlySpan<char> rwx) =>
        rwx switch
        {
            ['r', 'w', 'x'] => "Can view, modify, and execute",
            ['r', 'w', '-'] => "Can view and modify",
            ['r', '-', 'x'] => "Can view and execute",
            ['-', 'w', 'x'] => "Can modify and execute",
            ['r', '-', '-'] => "Can view",
            ['-', 'w', '-'] => "Can modify",
            ['-', '-', 'x'] => "Can execute",
            _ => "No permissions",
        };

    private static string GetDirPerms(ReadOnlySpan<char> rwx) =>
        rwx switch
        {
            ['r', 'w', 'x'] => "Can view and modify contents",
            ['r', 'w', '-'] => "Can view and modify contents",
            ['r', '-', 'x'] => "Can view contents",
            ['-', 'w', 'x'] => "Can modify contents",
            ['-', '-', 'x'] => "Can access directory",
            ['r', '-', '-'] => "Can view contents",
            _ => "No permissions",
        };

    public void Show()
    {
        PropertiesWindow window = new() { DataContext = this };
        window.ShowDialog(_mainWindow);
    }
}
