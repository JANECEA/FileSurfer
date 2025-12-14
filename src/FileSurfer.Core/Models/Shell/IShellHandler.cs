namespace FileSurfer.Core.Models.Shell;

/// <summary>
/// Represents the layer between the OS shell and the <see cref="FileSurfer"/> layer.
/// </summary>
public interface IShellHandler
{
    /// <summary>
    /// Creates a symbolic link to the specified file path.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult CreateLink(string filePath);

    /// <summary>
    /// Opens a file at the specified path in the application preferred by the OS.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult OpenFile(string filePath);

    /// <summary>
    /// Opens a file in the Notepad app at the path specified.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult OpenInNotepad(string filePath, string notepadPath);

    /// <summary>
    /// Opens a command prompt at the specified directory path.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult OpenCmdAt(string dirPath);

    /// <summary>
    /// Executes a command in the command prompt.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult ExecuteCmd(string command);
}
