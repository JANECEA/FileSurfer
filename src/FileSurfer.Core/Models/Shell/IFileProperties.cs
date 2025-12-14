namespace FileSurfer.Core.Models.Shell;

/// <summary>
/// Provides methods to interact with file properties and dialogs
/// </summary>
public interface IFileProperties
{
    /// <summary>
    /// Calls the <see cref="ShellExecuteEx(ref ShellExecuteInfo)"/> function to show the properties dialog of the specified <paramref name="filePath"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the properties dialog was successfully shown, otherwise <see langword="false"/>.</returns>
    public IResult ShowFileProperties(string filePath);

    /// <summary>
    /// Displays the "Open With" dialog for a specified file
    /// </summary>
    /// <returns><see langword="true"/> if the "Open With" dialog was successfully shown; otherwise, <see langword="false"/>.</returns>
    public IResult ShowOpenAsDialog(string filePath);
}
