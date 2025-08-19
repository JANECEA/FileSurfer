namespace FileSurfer.Models.Shell;

/// <summary>
/// Represents the layer between the OS shell and the <see cref="FileSurfer"/> layer.
/// </summary>
public interface IShellHandler
{
    /// <summary>
    /// Creates a symbolic link to the specified file path.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult CreateLink(string filePath);

    /// <summary>
    /// Opens a file at the specified path.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult OpenFile(string filePath);

    /// <summary>
    /// Opens a file in Notepad.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult OpenInNotepad(string filePath);

    /// <summary>
    /// Opens a command prompt at the specified directory path.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult OpenCmdAt(string dirPath);

    /// <summary>
    /// Executes a command in the command prompt.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult ExecuteCmd(string command);
}
