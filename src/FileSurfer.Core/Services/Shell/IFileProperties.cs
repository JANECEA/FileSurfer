using FileSurfer.Core.Models;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Services.Shell;

/// <summary>
/// Provides methods to interact with file properties and dialogs
/// </summary>
public interface IFileProperties
{
    /// <summary>
    /// Shows the file properties dialog
    /// </summary>
    /// <returns><see langword="true"/> if the properties dialog was successfully shown, otherwise <see langword="false"/>.</returns>
    public IResult ShowFileProperties(FileSystemEntryViewModel entry);

    /// <summary>
    /// Displays the "Open With" dialog for a specified file
    /// </summary>
    public bool SupportsOpenAs(IFileSystemEntry entry);

    /// <summary>
    /// Displays the "Open With" dialog for a specified file
    /// </summary>
    /// <returns><see langword="true"/> if the "Open With" dialog was successfully shown; otherwise, <see langword="false"/>.</returns>
    public IResult ShowOpenAsDialog(IFileSystemEntry entry);
}
