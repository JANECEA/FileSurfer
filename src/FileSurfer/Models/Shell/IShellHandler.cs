namespace FileSurfer.Models.Shell;

/// <summary>
/// Represents the layer between the OS shell and the <see cref="FileSurfer"/> layer.
/// </summary>
public interface IShellHandler
{
    /// <summary>
    /// Creates a symbolic link to the specified file path.
    /// </summary>
    /// <returns><see langword="true"/> if the link was successfully created, otherwise <see langword="false"/>.</returns>
    public bool CreateLink(string filePath, out string? errorMessage);

    /// <summary>
    /// Opens a file at the specified path.
    /// </summary>
    /// <returns><see langword="true"/> if the file was successfully opened, otherwise <see langword="false"/>.</returns>
    public bool OpenFile(string filePath, out string? errorMessage);

    /// <summary>
    /// Opens a file in Notepad.
    /// </summary>
    /// <returns><see langword="true"/> if the file was successfully opened, otherwise <see langword="false"/>.</returns>
    public bool OpenInNotepad(string filePath, out string? errorMessage);

    /// <summary>
    /// Opens a command prompt at the specified directory path.
    /// </summary>
    /// <returns><see langword="true"/> if the command prompt was successfully opened, otherwise <see langword="false"/>.</returns>
    public bool OpenCmdAt(string dirPath, out string? errorMessage);

    /// <summary>
    /// Executes a command in the command prompt.
    /// </summary>
    /// <returns><see langword="true"/> if the command was successfully executed, otherwise <see langword="false"/>.</returns>
    public bool ExecuteCmd(string command, out string? errorMessage);
}
