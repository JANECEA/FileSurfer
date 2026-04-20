using FileSurfer.Core.Models;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Services.Shell;

/// <summary>
/// Provides operations for showing platform-specific file property and "Open With" dialogs.
/// </summary>
public interface IFileProperties
{
    /// <summary>
    /// Shows the file properties dialog for the specified entry.
    /// </summary>
    /// <param name="entry">
    /// The file-system entry whose properties should be displayed.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if the dialog could not be shown.
    /// </returns>
    public IResult ShowFileProperties(FileSystemEntryViewModel entry);

    /// <summary>
    /// Determines whether the specified entry supports an "Open With" action.
    /// </summary>
    /// <param name="entry">
    /// The file-system entry to evaluate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the platform can present an "Open With" dialog for the entry;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool SupportsOpenAs(IFileSystemEntry entry);

    /// <summary>
    /// Shows the "Open With" dialog for the specified entry.
    /// </summary>
    /// <param name="entry">
    /// The file-system entry to open through a selected application.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if the dialog could not be shown.
    /// </returns>
    public IResult ShowOpenAsDialog(IFileSystemEntry entry);
}
