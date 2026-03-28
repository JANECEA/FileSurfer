using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.FileOperations.Undoable;

namespace FileSurfer.Core.Services.FileOperations;

/// <summary>
/// Represents the layer between the <see cref="FileSurfer"/> app and the system clipboard.
/// </summary>
public interface IClipboardManager
{
    /// <summary>
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<IResult> CutAsync(IFileSystemEntry[] selectedFiles, Location currentLocation);

    /// <summary>
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<IResult> CopyAsync(IFileSystemEntry[] selectedFiles, Location currentLocation);

    /// <summary>
    /// Pastes the contents of the system clipboard into <paramref name="destination"/>.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<ValueResult<IUndoableFileOperation?>> PasteAsync(Location destination);

    /// <summary>
    /// Copies the <paramref name="filePath"/> to the system's clipboard.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<IResult> CopyPathToFileAsync(string filePath);
}
