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
    private static readonly ValueResult<string> MissingRepoResult = ValueResult<string>.Error(
        "No git repository found."
    );
    private static readonly StatusOptions StatusOptions = new()
    {
        IncludeUntracked = true,
        RecurseUntrackedDirs = true,
        DisablePathSpecMatch = true,
        DetectRenamesInIndex = true,
        DetectRenamesInWorkDir = true,
        IncludeIgnored = false,
        IncludeUnaltered = false,
        RecurseIgnoredDirs = false,
    };

    private readonly IShellHandler _shellHandler;
    private readonly Dictionary<string, GitStatus> _pathStates = new();
    private Repository? _currentRepo;

    public LocalGitIntegration(IShellHandler shellHandler) => _shellHandler = shellHandler;

    public bool InitIfGitRepository(string directoryPath)
    {
        string? repoRoot = Repository.Discover(directoryPath);
        if (repoRoot is null)
        {
            _currentRepo?.Dispose();
            _currentRepo = null;
            return false;
        }
        if (LocalPathTools.PathsAreEqual(_currentRepo?.Info.Path, repoRoot))
        {
            SetFileStates();
            return true;
        }

        try
        {
            _currentRepo?.Dispose();
            _currentRepo = new Repository(repoRoot);
            SetFileStates();
            return true;
        }
        catch
        {
            _currentRepo = null;
            return false;
        }
    }

    private static string GetWorkingDir(Repository repo) =>
        LocalPathTools.NormalizePath(repo.Info.WorkingDirectory);

    public IResult FetchChanges()
    {
        if (_currentRepo is null)
            return MissingRepoResult;

        try
        {
            Remote? remote = _currentRepo.Head.TrackedBranch is Branch branch
                ? _currentRepo.Network.Remotes[branch.RemoteName]
                : _currentRepo.Network.Remotes["origin"];

            if (remote is null)
                return SimpleResult.Error("No tracking information found.");

            Commands.Fetch(
                _currentRepo,
                remote.Name,
                remote.FetchRefSpecs.Select(spec => spec.Specification),
                null,
                null
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public ValueResult<string> PullChanges() =>
        _currentRepo is not null
            ? _shellHandler.ExecuteCommand("git", "-C", GetWorkingDir(_currentRepo), "pull")
            : MissingRepoResult;

    public RepoDetails? GetRepositoryState()
    {
        BranchTrackingDetails? details = _currentRepo?.Head.TrackingDetails;
        if (details is null)
            return null;

        int? behind = details.BehindBy;
        int? ahead = details.AheadBy;
        if (behind is null || ahead is null)
            return null;

        return new RepoDetails(behind.Value, ahead.Value);
    }

    public string GetCurrentBranchName() =>
        _currentRepo is null ? string.Empty : _currentRepo.Head.FriendlyName;

    public string[] GetBranches() =>
        _currentRepo is null
            ? Array.Empty<string>()
            : _currentRepo.Branches.Where(b => !b.IsRemote).Select(b => b.FriendlyName).ToArray();

    public IResult SwitchBranches(string branchName)
    {
        if (_currentRepo is null)
            return MissingRepoResult;

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

        RepositoryStatus repoStatus = _currentRepo.RetrieveStatus(StatusOptions);

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
            status.HasFlag(FileStatus.NewInWorkdir)
            || status.HasFlag(FileStatus.ModifiedInWorkdir)
            || status.HasFlag(FileStatus.DeletedFromWorkdir)
            || status.HasFlag(FileStatus.RenamedInWorkdir)
            || status.HasFlag(FileStatus.TypeChangeInWorkdir)
        )
            return GitStatus.Unstaged;

        if (
            status.HasFlag(FileStatus.NewInIndex)
            || status.HasFlag(FileStatus.ModifiedInIndex)
            || status.HasFlag(FileStatus.DeletedFromIndex)
            || status.HasFlag(FileStatus.RenamedInIndex)
            || status.HasFlag(FileStatus.TypeChangeInIndex)
        )
            return GitStatus.Staged;

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
                return MissingRepoResult;

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
                return MissingRepoResult;

            Commands.Unstage(_currentRepo, filePath);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public ValueResult<string> CommitChanges(string commitMessage)
    {
        if (_currentRepo is null)
            return MissingRepoResult;

        if (!ValidateCommitMessage(commitMessage))
            return ValueResult<string>.Error($"Commit message: \"{commitMessage}\" is invalid.");

        return _shellHandler.ExecuteCommand(
            "git",
            "-C",
            GetWorkingDir(_currentRepo),
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

    public ValueResult<string> PushChanges() =>
        _currentRepo is not null
            ? _shellHandler.ExecuteCommand("git", "-C", GetWorkingDir(_currentRepo), "push")
            : MissingRepoResult;

    public void Dispose() => _currentRepo?.Dispose();
}
