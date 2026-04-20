using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations.Undoable;

namespace FileSurfer.Core.Services.FileOperations;

/// <summary>
/// Defines clipboard workflows used by FileSurfer, including cut/copy state capture, paste
/// execution with progress reporting, and path-text clipboard writes.
/// </summary>
public interface IClipboardManager
{
    /// <summary>
    /// Places the selected entries on the clipboard as a cut operation from the current location.
    /// </summary>
    /// <param name="selectedFiles">
    /// The entries selected for cutting.
    /// </param>
    /// <param name="currentLocation">
    /// The location from which the selected entries originate.
    /// </param>
    /// <returns>
    /// A task that returns the operation result, including any error details if cutting fails.
    /// </returns>
    public Task<IResult> CutAsync(IFileSystemEntry[] selectedFiles, Location currentLocation);

    /// <summary>
    /// Places the selected entries on the clipboard as a copy operation from the current location.
    /// </summary>
    /// <param name="selectedFiles">
    /// The entries selected for copying.
    /// </param>
    /// <param name="currentLocation">
    /// The location from which the selected entries originate.
    /// </param>
    /// <returns>
    /// A task that returns the operation result, including any error details if copying fails.
    /// </returns>
    public Task<IResult> CopyAsync(IFileSystemEntry[] selectedFiles, Location currentLocation);

    /// <summary>
    /// Pastes the contents of the system clipboard into <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">
    /// The target location where clipboard contents should be pasted.
    /// </param>
    /// <param name="reporter">
    /// Progress reporter used to publish paste progress updates.
    /// </param>
    /// <param name="ct">
    /// Cancellation token used to stop the paste operation.
    /// </param>
    /// <returns>
    /// A task that returns either an undoable file operation for successful paste actions or
    /// <see langword="null"/> when no undoable operation was produced, including error details on failure.
    /// </returns>
    public Task<ValueResult<IUndoableFileOperation?>> PasteAsync(
        Location destination,
        ProgressReporter reporter,
        CancellationToken ct
    );

    /// <summary>
    /// Copies the <paramref name="filePath"/> to the system's clipboard.
    /// </summary>
    /// <param name="filePath">
    /// The path text to copy to the system clipboard.
    /// </param>
    /// <returns>
    /// A task that returns the operation result, including any error details if clipboard update fails.
    /// </returns>
    public Task<IResult> CopyPathToFileAsync(string filePath);
}
