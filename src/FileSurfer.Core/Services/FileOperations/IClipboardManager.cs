using System.Threading.Tasks;

namespace FileSurfer.Core.Models.FileOperations;

/// <summary>
/// Describes different kinds of paste operation in the context of FileSurfer
/// </summary>
public enum PasteType
{
    /// <summary>
    /// Copying operation that is analogous to copying files and directories
    /// </summary>
    Copy,

    /// <summary>
    /// Cutting operation that is analogous to moving files and directories
    /// </summary>
    Cut,

    /// <summary>
    /// Duplicating operation that is analogous to duplicating files and directories
    /// </summary>
    Duplicate,
}

/// <summary>
/// Represents the layer between the <see cref="FileSurfer"/> app and the system clipboard.
/// </summary>
public interface IClipboardManager
{
    /// <summary>
    /// Retrieves the operation type based on the internal state of the manager and state of the system clipboard.
    /// </summary>
    public Task<PasteType> GetOperationType(string currentDir);

    /// <summary>
    /// Gets the contents of the internal clipboard.
    /// </summary>
    /// <returns>An array of <see cref="IFileSystemEntry"/>s.</returns>
    public IFileSystemEntry[] GetClipboard();

    /// <summary>
    /// Stores <paramref name="selectedFiles"/> to both the system and internal clipboards.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<IResult> CutAsync(IFileSystemEntry[] selectedFiles, string currentDir);

    /// <summary>
    /// Stores the selection of <see cref="IFileSystemEntry"/> in the internal and the system clipboards.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<IResult> CopyAsync(IFileSystemEntry[] selectedFiles, string currentDir);

    /// <summary>
    /// Pastes the contents of the system clipboard into <paramref name="currentDir"/>.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<ValueResult<IFileSystemEntry[]>> PasteAsync(string currentDir, PasteType pasteType);

    /// <summary>
    /// Duplicates the files stored in the clipboard.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<ValueResult<string[]>> DuplicateAsync(string currentDir);
}

/// <summary>
/// Represents a specialized version of the <see cref="IClipboardManager"/> for local operations
/// </summary>
public interface ILocalClipboardManager : IClipboardManager
{
    /// <summary>
    /// Copies the <paramref name="filePath"/> to the system's clipboard.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<IResult> CopyPathToFileAsync(string filePath);

    /// <summary>
    /// Tries to paste an image from the clipboard
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<ValueResult<IFileSystemEntry>> PasteImageAsync(string currentDir);
}
