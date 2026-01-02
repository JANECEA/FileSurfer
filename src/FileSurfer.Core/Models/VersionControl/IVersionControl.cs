using System;

namespace FileSurfer.Core.Models.VersionControl;

/// <summary>
/// Defines methods for interacting with a version control system withing the <see cref="FileSurfer"/> app.
/// </summary>
public interface IVersionControl : IDisposable
{
    /// <summary>
    /// Determines whether the specified directory is under version control.
    /// </summary>
    /// <param name="directoryPath">The path of the directory to check.</param>
    /// <returns><see langword="true"/> if the directory is part of a git repository or <see langword="false"/> otherwise.</returns>
    public bool InitIfVersionControlled(string directoryPath);

    /// <summary>
    /// Retrieves the status of the specified path in the version control system.
    /// </summary>
    /// <param name="filePath">The path for which to retrieve the status.</param>
    /// <returns><see cref="VcStatus"/> representing the version control status in the context of <see cref="FileSurfer"/>.</returns>
    public VcStatus GetStatus(string filePath);

    /// <summary>
    /// Downloads the latest changes from the version control system to the local repository.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult DownloadChanges();

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
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult SwitchBranches(string branchName);

    /// <summary>
    /// Stages changes on the specified path for the next commit.
    /// </summary>
    /// <param name="path">The path to stage.</param>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult StagePath(string path);

    /// <summary>
    /// Unstages changes in the specified file or directory, reverting it to the previous staged state.
    /// </summary>
    /// <param name="filePath">The path of the file or directory to unstage.</param>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult UnstagePath(string filePath);

    /// <summary>
    /// Commits the staged changes with the specified commit message.
    /// </summary>
    /// <param name="commitMessage">The message describing the commit.</param>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult CommitChanges(string commitMessage);

    /// <summary>
    /// Uploads the committed changes to the remote repository.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult UploadChanges();
}
