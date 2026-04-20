using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using FileSurfer.Core.Views;

namespace FileSurfer.Core.ViewModels;

/// <summary>
/// Represents an object that can present its own UI.
/// </summary>
public interface IDisplayable
{
    /// <summary>
    /// Opens or shows the underlying visual representation.
    /// </summary>
    public void Show();
}

/// <summary>
/// The PropertiesWindowViewModel is the ViewModel for the <see cref="PropertiesWindow"/>.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public sealed class PropertiesWindowViewModel : IDisplayable
{
    /// <summary>
    /// Gets the file-system entry view model displayed by the properties window.
    /// </summary>
    public FileSystemEntryViewModel EntryVm { get; }

    /// <summary>
    /// Gets the formatted size string shown in the properties window.
    /// </summary>
    public required string Size { get; init; }

    /// <summary>
    /// Gets the formatted creation timestamp string.
    /// </summary>
    public string? DateCreatedStr { get; private set; } = null;

    /// <summary>
    /// Sets the raw creation date used to populate <see cref="DateCreatedStr"/>.
    /// </summary>
    public required DateTime DateCreated
    {
        init => DateCreatedStr = GetDateString(value);
    }

    /// <summary>
    /// Gets the formatted last-access timestamp string.
    /// </summary>
    public string? DateAccessedStr { get; private set; } = null;

    /// <summary>
    /// Sets the raw last-access date used to populate <see cref="DateAccessedStr"/>.
    /// </summary>
    public required DateTime DateAccessed
    {
        init => DateAccessedStr = GetDateString(value);
    }

    /// <summary>
    /// Gets the formatted last-modified timestamp string.
    /// </summary>
    public string? DateModifiedStr { get; private set; } = null;

    /// <summary>
    /// Sets the raw last-modified date used to populate <see cref="DateModifiedStr"/>.
    /// </summary>
    public required DateTime DateModified
    {
        init => DateModifiedStr = GetDateString(value);
    }

    /// <summary>
    /// Gets the owner string displayed in the properties window.
    /// </summary>
    public required string Owner { get; init; }

    /// <summary>
    /// Gets the human-readable permission description for the owner.
    /// </summary>
    public string PermissionsOwner { get; }

    /// <summary>
    /// Gets the human-readable permission description for the group.
    /// </summary>
    public string PermissionsGroup { get; }

    /// <summary>
    /// Gets the human-readable permission description for others.
    /// </summary>
    public string PermissionsOther { get; }

    /// <summary>
    /// Creates a properties window view model and translates raw POSIX-style permission bits into
    /// user-facing permission descriptions.
    /// </summary>
    /// <param name="entry">
    /// The file-system entry the properties view model represents.
    /// </param>
    /// <param name="permissions">
    /// A nine-character permission string in rwx form (for owner, group, and others).
    /// </param>
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
        window.Show();
    }
}
