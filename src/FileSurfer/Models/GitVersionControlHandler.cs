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
public class GitVersionControlHandler : IVersionControl, IDisposable
{
    private const string MissingRepoMessage = "No git repository found";
    private readonly IFileIOHandler _fileIOHandler;
    private Repository? _currentRepo = null;
    private string? _currentRepoRootDir = null;
    private readonly Dictionary<string, VCStatus> _statusDict = new();

    /// <summary>
    /// Initializes a new <see cref="GitVersionControlHandler"/>.
    /// </summary>
    public GitVersionControlHandler(IFileIOHandler fileIOHandler) => _fileIOHandler = fileIOHandler;

    /// <inheritdoc/>
    public bool IsVersionControlled(string directoryPath)
    {
        string? repoRootDir = directoryPath;
        while (repoRootDir is not null)
        {
            string gitDir = Path.Combine(repoRootDir, ".git");
            if (!Directory.Exists(gitDir))
            {
                repoRootDir = Path.GetDirectoryName(repoRootDir);
                continue;
            }
            try
            {
                if (_currentRepo?.Info.Path != gitDir)
                {
                    _currentRepo?.Dispose();
                    _currentRepo = new Repository(repoRootDir);
                    _currentRepoRootDir = repoRootDir;
                    SetFileStates();
                }
                return true;
            }
            catch
            {
                break;
            }
        }
        _currentRepo?.Dispose();
        _currentRepo = null;
        _currentRepoRootDir = null;
        return false;
    }

    /// <inheritdoc/>
    public bool DownloadChanges(out string? errorMessage)
    {
        if (_currentRepo is null || _currentRepoRootDir is null)
        {
            errorMessage = MissingRepoMessage;
            return false;
        }
        string command = $"cd \"{_currentRepoRootDir}\" && git pull";
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
        if (_currentRepo is null || _currentRepoRootDir is null)
        {
            errorMessage = MissingRepoMessage;
            return false;
        }

        Branch branch = _currentRepo.Branches[branchName];
        if (branch is null)
        {
            errorMessage = $"branch: {branchName} not found";
            return false;
        }
        Commands.Checkout(_currentRepo, branch);

        errorMessage = null;
        return true;
    }

    private void SetFileStates()
    {
        if (_currentRepo is null || _currentRepoRootDir is null)
            return;

        RepositoryStatus repoStatus = _currentRepo.RetrieveStatus(
            new StatusOptions
            {
                IncludeUntracked = true,
                RecurseUntrackedDirs = true,
                DisablePathSpecMatch = true,
            }
        );
        _statusDict.Clear();
        foreach (StatusEntry? entry in repoStatus)
        {
            string absolutePath = Path.Combine(_currentRepoRootDir, entry.FilePath)
                .Replace('\\', '/');
            _statusDict[absolutePath] = ConvertToVCStatus(entry.State);
        }
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
    public VCStatus ConsolidateStatus(string path)
    {
        if (_currentRepo is null)
            return VCStatus.NotVersionControlled;

        return _statusDict.TryGetValue(path.Replace('\\', '/'), out VCStatus status)
            ? status
            : VCStatus.NotVersionControlled;
    }

    /// <inheritdoc/>
    public bool StageChange(string filePath, out string? errorMessage)
    {
        try
        {
            if (_currentRepo is null || _currentRepoRootDir is null)
            {
                errorMessage = MissingRepoMessage;
                return false;
            }
            string relativePath = Path.GetRelativePath(_currentRepoRootDir, filePath);
            _currentRepo.Index.Add(relativePath);
            _currentRepo.Index.Write();
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
    public bool UnstageChange(string filePath, out string? errorMessage)
    {
        try
        {
            if (_currentRepo is null || _currentRepoRootDir is null)
            {
                errorMessage = MissingRepoMessage;
                return false;
            }
            string relativePath = Path.GetRelativePath(
                _currentRepoRootDir,
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
        if (_currentRepo is null || _currentRepoRootDir is null)
        {
            errorMessage = MissingRepoMessage;
            return false;
        }
        string command =
            $"cd \"{_currentRepoRootDir}\" && git commit -m \"{commitMessage}\"";
        return _fileIOHandler.ExecuteCmd(command, out errorMessage);
    }

    /// <inheritdoc/>
    public bool UploadChanges(out string? errorMessage)
    {
        if (_currentRepo is null || _currentRepoRootDir is null)
        {
            errorMessage = MissingRepoMessage;
            return false;
        }
        string command = $"cd \"{_currentRepoRootDir}\" && git push";
        return _fileIOHandler.ExecuteCmd(command, out errorMessage);
    }

    /// <summary>
    /// Disposes of <see cref="_currentRepo"/>.
    /// </summary>
    public void Dispose()
    {
        _currentRepo?.Dispose();
        GC.SuppressFinalize(this);
    }
}
