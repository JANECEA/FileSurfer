using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace FileSurfer.Models;

/// <summary>
/// Consolidates complex Git file handling for the <see cref="FileSurfer"/> app's UI.
/// </summary>
public enum VCStatus
{
    /// <summary>
    /// File is either ignored or no changes have been made to it from the last commit.
    /// </summary>
    NotVersionControlled,

    /// <summary>
    /// File has been staged for the next commit.
    /// </summary>
    Staged,

    /// <summary>
    /// File has not been staged for the next commit.
    /// </summary>
    Unstaged,
}

/// <summary>
/// Handles git integration within the <see cref="FileSurfer"/> app.
/// </summary>
public class GitVersionControlHandler : IVersionControl
{
    private const string MissingRepoMessage = "No git repository found";
    private readonly IFileIOHandler _fileIOHandler;
    private Repository? _currentRepo = null;
    private readonly Dictionary<string, VCStatus> _fileStatuses = new();
    private readonly Dictionary<string, VCStatus> _dirStatuses = new();

    /// <summary>
    /// Initializes a new <see cref="GitVersionControlHandler"/>.
    /// </summary>
    public GitVersionControlHandler(IFileIOHandler fileIOHandler) => _fileIOHandler = fileIOHandler;

    /// <inheritdoc/>
    public bool InitIfVersionControlled(string directoryPath)
    {
        string? repoRootDir = directoryPath;
        string gitDir = string.Empty;
        while (repoRootDir is not null)
        {
            gitDir = Path.Combine(repoRootDir, ".git");
            if (Directory.Exists(gitDir))
                break;

            repoRootDir = Path.GetDirectoryName(repoRootDir);
        }

        if ((_currentRepo?.Info.Path) == gitDir + '\\')
        {
            SetFileStates();
            return true;
        }

        _currentRepo?.Dispose();
        if (repoRootDir is not null && Directory.Exists(repoRootDir))
        {
            try
            {
                _currentRepo = new Repository(repoRootDir);
                SetFileStates();
                return true;
            }
            catch { }
        }
        _currentRepo = null;
        return false;
    }

    private string? GetWorkingDir() => _currentRepo?.Info.WorkingDirectory.TrimEnd('\\');

    /// <inheritdoc/>
    public bool DownloadChanges(out string? errorMessage)
    {
        if (_currentRepo is null)
        {
            errorMessage = MissingRepoMessage;
            return false;
        }
        string command = $"git -C \"{GetWorkingDir()}\" pull";
        return _fileIOHandler.ExecuteCmd(command, out errorMessage);
    }

    /// <inheritdoc/>
    public string GetCurrentBranchName() =>
        _currentRepo is null ? string.Empty : _currentRepo.Head.FriendlyName;

    /// <inheritdoc/>
    public string[] GetBranches() =>
        _currentRepo is null
            ? Array.Empty<string>()
            : _currentRepo.Branches.Where(b => !b.IsRemote).Select(b => b.FriendlyName).ToArray();

    /// <inheritdoc/>
    public bool SwitchBranches(string branchName, out string? errorMessage)
    {
        if (_currentRepo is null)
        {
            errorMessage = MissingRepoMessage;
            return false;
        }

        Branch branch = _currentRepo.Branches[branchName];
        if (branch is null)
        {
            errorMessage = $"branch: \"{branchName}\" not found";
            return false;
        }
        Commands.Checkout(_currentRepo, branch);

        errorMessage = null;
        return true;
    }

    private void SetFileStates()
    {
        if (_currentRepo is null)
            return;

        RepositoryStatus repoStatus = _currentRepo.RetrieveStatus(
            new StatusOptions
            {
                IncludeUntracked = true,
                RecurseUntrackedDirs = true,
                DisablePathSpecMatch = true,
                DetectRenamesInIndex = true,
                DetectRenamesInWorkDir = true,
                IncludeIgnored = false,
                IncludeUnaltered = false,
                RecurseIgnoredDirs = false,
            }
        );

        _fileStatuses.Clear();
        _dirStatuses.Clear();
        foreach (StatusEntry? entry in repoStatus)
        {
            string absolutePath = Path.Combine(_currentRepo.Info.WorkingDirectory, entry.FilePath)
                .Replace('/', '\\');
            VCStatus status = ConvertToVCStatus(entry.State);
            _fileStatuses[absolutePath] = status;

            if (status is VCStatus.Unstaged or VCStatus.Staged)
                SetDirStatuses(absolutePath, status);
        }
    }

    private void SetDirStatuses(string absolutePath, VCStatus status)
    {
        string? parentPath = absolutePath;
        while (
            (parentPath = Path.GetDirectoryName(parentPath))?.Length
            > _currentRepo!.Info.WorkingDirectory.Length
        )
            if (status is VCStatus.Unstaged)
                _dirStatuses[parentPath] = VCStatus.Unstaged;
            else if (
                !_dirStatuses.TryGetValue(parentPath, out VCStatus currentStatus)
                || currentStatus is not VCStatus.Unstaged
            )
                _dirStatuses[parentPath] = VCStatus.Staged;
    }

    private static VCStatus ConvertToVCStatus(FileStatus status)
    {
        if (status is FileStatus.Ignored or FileStatus.Nonexistent or FileStatus.Unaltered)
            return VCStatus.NotVersionControlled;

        if (
            status.HasFlag(FileStatus.NewInIndex)
            || status.HasFlag(FileStatus.ModifiedInIndex)
            || status.HasFlag(FileStatus.DeletedFromIndex)
            || status.HasFlag(FileStatus.RenamedInIndex)
            || status.HasFlag(FileStatus.TypeChangeInIndex)
        )
            return VCStatus.Staged;

        if (
            status.HasFlag(FileStatus.NewInWorkdir)
            || status.HasFlag(FileStatus.ModifiedInWorkdir)
            || status.HasFlag(FileStatus.DeletedFromWorkdir)
            || status.HasFlag(FileStatus.RenamedInWorkdir)
            || status.HasFlag(FileStatus.TypeChangeInWorkdir)
        )
            return VCStatus.Unstaged;

        return VCStatus.NotVersionControlled;
    }

    /// <inheritdoc/>
    public VCStatus GetFileStatus(string filePath) =>
        _currentRepo is not null
            ? _fileStatuses.GetValueOrDefault(filePath, VCStatus.NotVersionControlled)
            : VCStatus.NotVersionControlled;

    /// <inheritdoc/>
    public VCStatus GetDirStatus(string dirPath) =>
        _currentRepo is not null
            ? _dirStatuses.GetValueOrDefault(dirPath, VCStatus.NotVersionControlled)
            : VCStatus.NotVersionControlled;

    /// <inheritdoc/>
    public bool StagePath(string path, out string? errorMessage)
    {
        try
        {
            if (_currentRepo is null)
            {
                errorMessage = MissingRepoMessage;
                return false;
            }

            Commands.Stage(_currentRepo, path);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <inheritdoc/>
    public bool UnstagePath(string filePath, out string? errorMessage)
    {
        try
        {
            if (_currentRepo is null)
            {
                errorMessage = MissingRepoMessage;
                return false;
            }
            string relativePath = Path.GetRelativePath(
                _currentRepo.Info.WorkingDirectory,
                filePath
            );
            Commands.Unstage(_currentRepo, relativePath);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <inheritdoc/>
    public bool CommitChanges(string commitMessage, out string? errorMessage)
    {
        if (_currentRepo is null)
        {
            errorMessage = MissingRepoMessage;
            return false;
        }
        string command = $"git -C \"{GetWorkingDir()}\" commit -m \"{commitMessage}\"";
        return _fileIOHandler.ExecuteCmd(command, out errorMessage);
    }

    /// <inheritdoc/>
    public bool UploadChanges(out string? errorMessage)
    {
        if (_currentRepo is null)
        {
            errorMessage = MissingRepoMessage;
            return false;
        }
        string command = $"git -C \"{GetWorkingDir()}\" push";
        return _fileIOHandler.ExecuteCmd(command, out errorMessage);
    }

    /// <summary>
    /// Disposes of <see cref="_currentRepo"/>.
    /// </summary>
    public void Dispose() => _currentRepo?.Dispose();
}
