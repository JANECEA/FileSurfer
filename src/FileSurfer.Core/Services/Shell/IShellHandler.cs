using System.Threading.Tasks;
using FileSurfer.Core.Models;

namespace FileSurfer.Core.Services.Shell;

/// <summary>
/// Provides operations for invoking shell commands and shell-backed file-system actions.
/// </summary>
public interface IShellHandler
{
    /// <summary>
    /// Creates a symbolic link for the specified file path.
    /// </summary>
    /// <param name="filePath">
    /// The full path to the source file that should be linked.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if link creation fails.
    /// </returns>
    public IResult CreateFileLink(string filePath);

    /// <summary>
    /// Creates a symbolic link for the specified directory path.
    /// </summary>
    /// <param name="dirPath">
    /// The full path to the source directory that should be linked.
    /// </param>
    /// <returns>
    /// The operation result, including any error details if link creation fails.
    /// </returns>
    public IResult CreateDirectoryLink(string dirPath);

    /// <summary>
    /// Executes a command and returns <c>stdout</c>.
    /// <br/>
    /// If <c>stdout</c> is empty, <c>stderr</c> is returned instead.
    /// </summary>
    /// <param name="programName">
    /// The executable or command name to run.
    /// </param>
    /// <param name="args">
    /// Arguments passed to the command in execution order.
    /// </param>
    /// <returns>
    /// A command result containing output text on success or error details on failure.
    /// </returns>
    public ValueResult<string> ExecuteCommand(string programName, params string[] args);

    /// <summary>
    /// Asynchronously executes a command and returns <c>stdout</c>.
    /// <br/>
    /// If <c>stdout</c> is empty, <c>stderr</c> is returned instead.
    /// </summary>
    /// <param name="programName">
    /// The executable or command name to run.
    /// </param>
    /// <param name="args">
    /// Arguments passed to the command in execution order.
    /// </param>
    /// <returns>
    /// A task that returns command output on success or error details on failure.
    /// </returns>
    public Task<ValueResult<string>> ExecuteCommandAsync(string programName, params string[] args);
}

/// <summary>
/// Extends <see cref="IShellHandler"/> with local desktop shell integration features.
/// </summary>
public interface ILocalShellHandler : IShellHandler
{
    /// <summary>
    /// Opens a terminal window rooted at the specified directory path.
    /// </summary>
    /// <param name="dirPath">
    /// The directory path that the opened terminal should use as its working directory.
    /// </param>
    /// <returns>
    /// The operation result, including any launch error details.
    /// </returns>
    public IResult OpenTerminalAt(string dirPath);

    /// <summary>
    /// Opens the specified file using the operating system's default associated application.
    /// </summary>
    /// <param name="filePath">
    /// The full path to the file that should be opened.
    /// </param>
    /// <returns>
    /// The operation result, including any launch error details.
    /// </returns>
    public IResult OpenFile(string filePath);

    /// <summary>
    /// Opens the specified file in the configured text editor (referred to as Notepad in settings).
    /// </summary>
    /// <param name="filePath">
    /// The full path to the file that should be opened in the configured editor.
    /// </param>
    /// <returns>
    /// The operation result, including any launch error details.
    /// </returns>
    public IResult OpenInNotepad(string filePath);
}
