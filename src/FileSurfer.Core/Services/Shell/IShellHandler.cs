using FileSurfer.Core.Models;

namespace FileSurfer.Core.Services.Shell;

/// <summary>
/// Provides methods for interacting with the system shell
/// </summary>
public interface IShellHandler
{
    /// <summary>
    /// Creates a symbolic link to the specified file path.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult CreateFileLink(string filePath);

    /// <summary>
    /// Creates a symbolic link to the specified directory path.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult CreateDirectoryLink(string dirPath);

    /// <summary>
    /// Executes a command in the command prompt.
    /// </summary>
    /// <param name="programName">Program to execute</param>
    /// <param name="args">Arguments for the command's $variables</param>
    /// <returns>A <see cref="ValueResult{string}"/> representing the result stdout of the operation and potential errors.</returns>
    public ValueResult<string> ExecuteCommand(string programName, params string[] args);
}

/// <summary>
/// Provides methods for interacting with the local system shell
/// </summary>
public interface ILocalShellHandler : IShellHandler
{
    /// <summary>
    /// Opens a command prompt at the specified directory path.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult OpenCmdAt(string dirPath);

    /// <summary>
    /// Opens a file at the specified path in the application preferred by the OS.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult OpenFile(string filePath);

    /// <summary>
    /// Opens a file in the Notepad app at the path specified.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult OpenInNotepad(string filePath);
}
