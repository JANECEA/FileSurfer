using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileSurfer.Core.Extensions;
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
        DetectRenamesInIndex = false,
        DetectRenamesInWorkDir = false,
        IncludeIgnored = false,
        IncludeUnaltered = false,
        RecurseIgnoredDirs = false,
    };
    private static readonly CheckoutOptions CheckoutOpts = new()
    {
        CheckoutModifiers = CheckoutModifiers.Force,
    };
    private static readonly StashApplyOptions StashApplyOptions = new()
    {
        ApplyModifiers = StashApplyModifiers.ReinstateIndex,
    };

    private readonly IShellHandler _shellHandler;
    private readonly Dictionary<string, GitStatus> _pathStates = new();
    private Repository? _currentRepo;

    public LocalGitIntegration(IShellHandler shellHandler) => _shellHandler = shellHandler;

    private string[] GetWholeCommand(string[] restOfCommand)
    {
        string[] commandStart = ["-C", GetWorkingDir(_currentRepo!)];
        string[] wholeCommand = new string[commandStart.Length + restOfCommand.Length];

        for (int i = 0; i < commandStart.Length; i++)
            wholeCommand[i] = commandStart[i];

        for (int i = 0; i < restOfCommand.Length; i++)
            wholeCommand[i + commandStart.Length] = restOfCommand[i];

        return wholeCommand;
    }

    private ValueResult<string> ExecuteGitCommand(params string[] restOfCommand) =>
        _shellHandler.ExecuteCommand("git", GetWholeCommand(restOfCommand));

    private Task<ValueResult<string>> ExecuteGitCommandAsync(params string[] restOfCommand) =>
        _shellHandler.ExecuteCommandAsync("git", GetWholeCommand(restOfCommand));

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

    public async Task<IResult> FetchChangesAsync()
    {
        if (_currentRepo is null)
            return MissingRepoResult;

        try
        {
            return await ExecuteGitCommandAsync("fetch");
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public Task<ValueResult<string>> PullChangesAsync()
    {
        if (_currentRepo is null)
            return Task.FromResult(MissingRepoResult);

        Branch branch = _currentRepo.Head;
        if (branch.TrackedBranch is not null)
            return ExecuteGitCommandAsync("pull");

        Remote? origin = _currentRepo.Network.Remotes["origin"];
        if (origin is null)
            return Task.FromResult(
                ValueResult<string>.Error("No remote configured for this repository.")
            );

        return ExecuteGitCommandAsync("pull", "origin", branch.FriendlyName);
    }

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
            Branch? branch = _currentRepo.Branches[branchName];
            if (branch is null)
                return SimpleResult.Error($"Could not find branch \"{branchName}\".");

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
        if (_currentRepo is null)
            return MissingRepoResult;

        path = LocalPathTools.NormalizePath(path);
        try
        {
            if (LocalPathTools.PathsAreEqualNormalized(path, GetWorkingDir(_currentRepo)))
                path = "*";

            Commands.Stage(_currentRepo, path);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult UnstagePath(string path)
    {
        if (_currentRepo is null)
            return MissingRepoResult;

        path = LocalPathTools.NormalizePath(path);
        try
        {
            if (LocalPathTools.PathsAreEqualNormalized(path, GetWorkingDir(_currentRepo)))
                path = "*";

            Commands.Unstage(_currentRepo, path);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult StashChanges()
    {
        if (_currentRepo is null)
            return MissingRepoResult;

        try
        {
            Signature? signature = _currentRepo.Config.BuildSignature(DateTimeOffset.Now);
            if (signature is null)
                return SimpleResult.Error("Configuration not found.");

            Stash? stash = _currentRepo.Stashes.Add(
                signature,
                "Stash invoked by: FileSurfer",
                StashModifiers.IncludeUntracked
            );
            return stash is null
                ? SimpleResult.Error("No local changes to save.")
                : SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult PopChanges()
    {
        if (_currentRepo is null)
            return MissingRepoResult;

        try
        {
            StashApplyStatus status = _currentRepo.Stashes.Pop(0, StashApplyOptions);
            return status switch
            {
                StashApplyStatus.Applied => SimpleResult.Ok(),
                StashApplyStatus.Conflicts => SimpleResult.Error(
                    """
                    Applying the stash would result in conflicts.
                    Commit or stash your current changes and try again.
                    """
                ),
                StashApplyStatus.NotFound => SimpleResult.Error("No stash entries found."),
                StashApplyStatus.UncommittedChanges => SimpleResult.Error(
                    """
                    Your local changes to the following files would be overwritten by stash pop.
                    Commit or stash them first.
                    """
                ),
                _ => throw new ArgumentOutOfRangeException(nameof(status)),
            };
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult RestorePath(string path)
    {
        if (_currentRepo is null)
            return MissingRepoResult;

        path = LocalPathTools.NormalizePath(path);
        try
        {
            if (LocalPathTools.PathsAreEqualNormalized(path, GetWorkingDir(_currentRepo)))
            {
                Commands.Checkout(_currentRepo, "HEAD", CheckoutOpts);
                return ExecuteGitCommand("clean", "-fd");
            }
            _currentRepo.CheckoutPaths("HEAD", [path], CheckoutOpts);
            return ExecuteGitCommand("clean", "-fd", "--", path);
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public Task<ValueResult<string>> CommitChangesAsync(string commitMessage)
    {
        if (_currentRepo is null)
            return Task.FromResult(MissingRepoResult);

        if (!ValidateCommitMessage(commitMessage))
            return Task.FromResult(
                ValueResult<string>.Error($"Commit message: \"{commitMessage}\" is invalid.")
            );

        return ExecuteGitCommandAsync("commit", "-m", commitMessage.Trim());
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

    private Task<ValueResult<string>> PushChangesInternal()
    {
        if (_currentRepo is null)
            return Task.FromResult(MissingRepoResult);

        Branch currentBranch = _currentRepo.Head;
        if (currentBranch.TrackedBranch is not null)
            return ExecuteGitCommandAsync("push");

        Remote? origin = _currentRepo.Network.Remotes["origin"];
        if (origin is null)
            return Task.FromResult(
                ValueResult<string>.Error("No remote configured for this repository.")
            );

        return ExecuteGitCommandAsync("push", "origin", currentBranch.FriendlyName);
    }

    public async Task<ValueResult<string>> PushChangesAsync()
    {
        ValueResult<string> result = await PushChangesInternal();
        if (result.IsOk && string.IsNullOrWhiteSpace(result.Value))
            result = "Changes pushed successfully.".OkResult();

        return result;
    }

    public void Dispose() => _currentRepo?.Dispose();
}
