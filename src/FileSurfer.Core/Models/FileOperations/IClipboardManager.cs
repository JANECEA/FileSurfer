using System.Threading.Tasks;

namespace FileSurfer.Core.Models.FileOperations;

/// <summary>
/// Represents the layer between the <see cref="FileSurfer"/> app and the system clipboard.
/// </summary>
public interface IClipboardManager
{
    /// <summary>
    /// Indicates if clipboard's contents are meant to be cut or copied from their original location.
    /// </summary>
    public bool IsCutOperation { get; }

    /// <summary>
    /// Determines if the current copy operation is occuring in the same directory.
    /// </summary>
    /// <param name="currentDir"></param>
    /// <returns><see langword="true"/> if directory to copy form and <paramref name="currentDir"/> are equal, otherwise <see langword="false"/>.</returns>
    public bool IsDuplicateOperation(string currentDir);

    /// <summary>
    /// Stores <paramref name="selectedFiles"/> to both the system and internal clipboards.
    /// <para>
    /// Sets <see cref="IsCutOperation"/> to <see langword="true"/>.
    /// </para>
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<IResult> CutAsync(IFileSystemEntry[] selectedFiles, string currentDir);

    /// <summary>
    /// Stores the selection of <see cref="IFileSystemEntry"/> in the internal and the system clipboards.
    /// <para>
    /// Sets <see cref="IsCutOperation"/> to <see langword="false"/>.
    /// </para>
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<IResult> CopyAsync(IFileSystemEntry[] selectedFiles, string currentDir);

    /// <summary>
    /// Pastes the contents of the system clipboard into <paramref name="currentDir"/>.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<IResult> PasteAsync(string currentDir);

    /// <summary>
    /// Duplicates the files stored in the clipboard.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult Duplicate(string currentDir, out string[] copyNames);

    /// <summary>
    /// Copies the <paramref name="filePath"/> to the system's clipboard.
    /// </summary>
    public Task CopyPathToFileAsymc(string filePath);

    /// <summary>
    /// Gets the contents of the internal clipboard.
    /// </summary>
    /// <returns>An array of <see cref="IFileSystemEntry"/>s.</returns>
    public IFileSystemEntry[] GetClipboard();
}
