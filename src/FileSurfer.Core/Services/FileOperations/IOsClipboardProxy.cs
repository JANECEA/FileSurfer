using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using FileSurfer.Core.Models;

namespace FileSurfer.Core.Services.FileOperations;

/// <summary>
/// Provides abstracted access to the operating system clipboard with support for text, images, and file-system entries.
/// </summary>
public interface IOsClipboardProxy
{
    /// <summary>
    /// Asynchronously sets the specified text to the OS clipboard.
    /// </summary>
    /// <param name="text">
    /// The text to place on the clipboard.
    /// </param>
    /// <returns>
    /// A task that returns the operation result, including error details if the clipboard set operation fails.
    /// </returns>
    Task<IResult> SetTextAsync(string text);

    /// <summary>
    /// Asynchronously attempts to retrieve a bitmap image from the OS clipboard.
    /// </summary>
    /// <returns>
    /// A task that returns the bitmap if available on the clipboard; otherwise <see langword="null"/>.
    /// </returns>
    Task<Bitmap?> TryGetBitmapAsync();

    /// <summary>
    /// Asynchronously attempts to retrieve text from the OS clipboard.
    /// </summary>
    /// <returns>
    /// A task that returns the text if available on the clipboard; otherwise <see langword="null"/>.
    /// </returns>
    Task<string?> TryGetTextAsync();

    /// <summary>
    /// Asynchronously attempts to retrieve file-system entries from the OS clipboard.
    /// </summary>
    /// <returns>
    /// A task that returns an array of file-system entries if available on the clipboard; otherwise <see langword="null"/>.
    /// </returns>
    Task<IFileSystemEntry[]?> TryGetFilesAsync();

    /// <summary>
    /// Asynchronously clears the OS clipboard on the UI thread by setting a marker object.
    /// </summary>
    /// <returns>
    /// A task that completes when the clearing operation has finished.
    /// </returns>
    Task<IResult> ClearAsync();

    /// <summary>
    /// Copies FileSurfer entries to the OS clipboard as storage items.
    /// </summary>
    /// <param name="entries">
    /// The file-system entries to place on the OS clipboard.
    /// </param>
    /// <returns>
    /// A task that returns the operation result, including error details when clipboard population fails.
    /// </returns>
    Task<IResult> CopyToOsClipboardAsync(IFileSystemEntry[] entries);
}
