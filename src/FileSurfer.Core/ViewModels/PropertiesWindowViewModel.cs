using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Views;

namespace FileSurfer.Core.ViewModels;

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
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public sealed class PropertiesWindowViewModel : IDisplayable
{
    public FileSystemEntryViewModel EntryVm { get; }
    public required string Size { get; init; }

    public string? DateCreatedStr { get; private set; } = null;
    public required DateTime DateCreated
    {
        init => DateCreatedStr = GetDateString(value);
    }

    public string? DateAccessedStr { get; private set; } = null;
    public required DateTime DateAccessed
    {
        init => DateAccessedStr = GetDateString(value);
    }

    public string? DateModifiedStr { get; private set; } = null;
    public required DateTime DateModified
    {
        init => DateModifiedStr = GetDateString(value);
    }

    public required string Owner { get; init; }
    public string PermissionsOwner { get; }
    public string PermissionsGroup { get; }
    public string PermissionsOther { get; }

    public PropertiesWindowViewModel(FileSystemEntryViewModel entry, string permissions)
    {
        EntryVm = entry;

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

    private static string GetDateString(DateTime date)
    {
        try
        {
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            DateTimeFormatInfo formatInfo = cultureInfo.DateTimeFormat;
            string format = $"{formatInfo.LongDatePattern} {formatInfo.LongTimePattern}";

            return date.ToString(format, cultureInfo);
        }
        catch (IOException)
        {
            return "Error";
        }
    }

    public void Show()
    {
        PropertiesWindow window = new() { DataContext = this };
        if (AvaloniaDialogService.MainWindow is null)
            window.Show();
        else
            window.ShowDialog(AvaloniaDialogService.MainWindow);
    }
}
