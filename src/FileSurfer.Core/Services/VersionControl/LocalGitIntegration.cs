using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;
using LibGit2Sharp;

namespace FileSurfer.Core.Services.VersionControl;

/// <summary>
/// Handles git integration within the <see cref="FileSurfer"/> app.
/// </summary>
public class LocalGitIntegration : IGitIntegration
{
    private const string MissingRepoMessage = "No git repository found";

    private readonly IShellHandler _shellHandler;

    private readonly Dictionary<string, GitStatus> _pathStates = new();
    private Repository? _currentRepo;

    /// <summary>
    /// Initializes a new <see cref="LocalGitIntegration"/>.
    /// </summary>
    public LocalGitIntegration(IShellHandler shellHandler) => _shellHandler = shellHandler;

    public bool InitIfGitRepository(string directoryPath)
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
        if (LocalPathTools.PathsAreEqual(_currentRepo?.Info.Path, gitDir))
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
            catch
            {
                // Not a valid Git repository
            }
        }
        _currentRepo = null;
        return false;
    }

    private string? GetWorkingDir() =>
        _currentRepo is not null
            ? LocalPathTools.NormalizePath(_currentRepo.Info.WorkingDirectory)
            : null;

    public IResult PullChanges() =>
        _currentRepo is null
            ? SimpleResult.Error(MissingRepoMessage)
            : _shellHandler.ExecuteCommand("git", "-C", GetWorkingDir()!, "pull");

    public string GetCurrentBranchName() =>
        _currentRepo is null ? string.Empty : _currentRepo.Head.FriendlyName;

    public string[] GetBranches() =>
        _currentRepo is null
            ? Array.Empty<string>()
            : _currentRepo.Branches.Where(b => !b.IsRemote).Select(b => b.FriendlyName).ToArray();

    public IResult SwitchBranches(string branchName)
    {
        if (_currentRepo is null)
            return SimpleResult.Error(MissingRepoMessage);

        try
        {
            Branch branch = _currentRepo.Branches[branchName];
            Commands.Checkout(_currentRepo, branch);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
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

        _pathStates.Clear();
        foreach (StatusEntry? entry in repoStatus)
        {
            string absolutePath = LocalPathTools.NormalizePath(
                Path.Combine(_currentRepo.Info.WorkingDirectory, entry.FilePath)
            );
            GitStatus status = ConvertToVcStatus(entry.State);
            _pathStates[absolutePath] = status;

            if (status is GitStatus.Unstaged or GitStatus.Staged)
                SetDirStatuses(absolutePath, status);
        }
    }

    private void SetDirStatuses(string absolutePath, GitStatus status)
    {
        string? parentPath = absolutePath;
        while (
            (parentPath = Path.GetDirectoryName(parentPath))?.Length
            > _currentRepo!.Info.WorkingDirectory.Length
        )
            if (status is GitStatus.Unstaged)
                _pathStates[parentPath] = GitStatus.Unstaged;
            else if (
                !_pathStates.TryGetValue(parentPath, out GitStatus currentStatus)
                || currentStatus is not GitStatus.Unstaged
            )
                _pathStates[parentPath] = GitStatus.Staged;
    }

    private static GitStatus ConvertToVcStatus(FileStatus status)
    {
        if (status is FileStatus.Ignored or FileStatus.Nonexistent or FileStatus.Unaltered)
            return GitStatus.NotVersionControlled;

        if (
            status.HasFlag(FileStatus.NewInIndex)
            || status.HasFlag(FileStatus.ModifiedInIndex)
            || status.HasFlag(FileStatus.DeletedFromIndex)
            || status.HasFlag(FileStatus.RenamedInIndex)
            || status.HasFlag(FileStatus.TypeChangeInIndex)
        )
            return GitStatus.Staged;

        if (
            status.HasFlag(FileStatus.NewInWorkdir)
            || status.HasFlag(FileStatus.ModifiedInWorkdir)
            || status.HasFlag(FileStatus.DeletedFromWorkdir)
            || status.HasFlag(FileStatus.RenamedInWorkdir)
            || status.HasFlag(FileStatus.TypeChangeInWorkdir)
        )
            return GitStatus.Unstaged;

        return GitStatus.NotVersionControlled;
    }

    public GitStatus GetStatus(string filePath) =>
        _currentRepo is not null
            ? _pathStates.GetValueOrDefault(
                LocalPathTools.NormalizePath(filePath),
                GitStatus.NotVersionControlled
            )
            : GitStatus.NotVersionControlled;

    public IResult StagePath(string path)
    {
        try
        {
            if (_currentRepo is null)
                return SimpleResult.Error(MissingRepoMessage);

            Commands.Stage(_currentRepo, path);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult UnstagePath(string filePath)
    {
        try
        {
            if (_currentRepo is null)
                return SimpleResult.Error(MissingRepoMessage);

            Commands.Unstage(_currentRepo, filePath);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult CommitChanges(string commitMessage)
    {
        if (_currentRepo is null)
            return SimpleResult.Error(MissingRepoMessage);

        if (!ValidateCommitMessage(commitMessage))
            return SimpleResult.Error($"Commit message: \"{commitMessage}\" is invalid.");

        return _shellHandler.ExecuteCommand(
            "git",
            "-C",
            GetWorkingDir()!,
            "commit",
            "-m",
            commitMessage.Trim()
        );
    }

    private static bool ValidateCommitMessage(string commitMessage)
    {
        if (string.IsNullOrWhiteSpace(commitMessage))
            return false;

        foreach (char c in commitMessage)
            if (c == '\0' || c == '"' || (c < 0x20 && c != '\t'))
                return false;

        return true;
    }

    public IResult PushChanges() =>
        _currentRepo is null
            ? SimpleResult.Error(MissingRepoMessage)
            : _shellHandler.ExecuteCommand("git", "-C", GetWorkingDir()!, "push");

    /// <summary>
    /// Disposes of <see cref="_currentRepo"/>.
    /// </summary>
    public void Dispose() => _currentRepo?.Dispose();
}
