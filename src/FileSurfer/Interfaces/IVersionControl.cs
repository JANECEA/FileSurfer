using System;

namespace FileSurfer.Models;

/// <summary>
/// Defines methods for interacting with a version control system withing the <see cref="FileSurfer"/> app.
/// </summary>
public interface IVersionControl : IDisposable
{
    /// <summary>
    /// Determines whether the specified directory is under version control.
    /// </summary>
    /// <param name="directoryPath">The path of the directory to check.</param>
    /// <returns><see langword="true"/> if the directory is version controlled; otherwise, <see langword="false"/>.</returns>
    public bool InitIfVersionControlled(string directoryPath);

    /// <summary>
    /// Retrieves the status of the specified file in the version control system.
    /// </summary>
    /// <param name="filePath">The path for which to retrieve the status.</param>
    /// <returns><see cref="VCStatus"/> representing the version control status in the context of <see cref="FileSurfer"/>.</returns>
    public VCStatus GetFileStatus(string filePath);

    /// <summary>
    /// Retrieves the status of the specified directory in the version control system.
    /// </summary>
    /// <param name="dirPath">The path for which to retrieve the status.</param>
    /// <returns><see cref="VCStatus"/> representing the version control status in the context of <see cref="FileSurfer"/>.</returns>
    public VCStatus GetDirStatus(string dirPath);

    /// <summary>
    /// Downloads the latest changes from the version control system to the local repository.
    /// </summary>
    /// <param name="errorMessage">An output parameter that will contain an error message if the download fails.</param>
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
    public bool DownloadChanges(out string? errorMessage);

    /// <summary>
    /// Retrieves a list of all branches in the version control system.
    /// </summary>
    /// <returns>An array of branch names.</returns>
    public string[] GetBranches();

    /// <summary>
    /// Retrieves the name of the current branch.
    /// </summary>
    /// <returns>The name of the current branch.</returns>
    public string GetCurrentBranchName();

    /// <summary>
    /// Switches to the specified branch in the version control system.
    /// </summary>
    /// <param name="branchName">The name of the branch to switch to.</param>
    /// <param name="errorMessage">An output parameter that will contain an error message if the switch fails.</param>
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
    public bool SwitchBranches(string branchName, out string? errorMessage);

    /// <summary>
    /// Stages changes on the specified path for the next commit.
    /// </summary>
    /// <param name="path">The path to stage.</param>
    /// <param name="errorMessage">An output parameter that will contain an error message if the staging fails.</param>
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
    public bool StagePath(string path, out string? errorMessage);

    /// <summary>
    /// Unstages changes in the specified file or directory, reverting it to the previous staged state.
    /// </summary>
    /// <param name="filePath">The path of the file or directory to unstage.</param>
    /// <param name="errorMessage">An output parameter that will contain an error message if the un-staging fails.</param>
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
    public bool UnstagePath(string filePath, out string? errorMessage);

    /// <summary>
    /// Commits the staged changes with the specified commit message.
    /// </summary>
    /// <param name="commitMessage">The message describing the commit.</param>
    /// <param name="errorMessage">An output parameter that will contain an error message if the commit fails.</param>
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
    public bool CommitChanges(string commitMessage, out string? errorMessage);

    /// <summary>
    /// Uploads the committed changes to the remote repository.
    /// </summary>
    /// <param name="errorMessage">An output parameter that will contain an error message if the upload fails.</param>
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
    public bool UploadChanges(out string? errorMessage);
}
