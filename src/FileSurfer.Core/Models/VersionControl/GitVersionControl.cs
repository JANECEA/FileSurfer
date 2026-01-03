using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileSurfer.Core.Models.Shell;
using LibGit2Sharp;

namespace FileSurfer.Core.Models.VersionControl;

/// <summary>
/// Handles git integration within the <see cref="FileSurfer"/> app.
/// </summary>
public class GitVersionControl : IVersionControl
{
    private const string MissingRepoMessage = "No git repository found";

    private readonly IShellHandler _shellHandler;

    private readonly Dictionary<string, VcStatus> _pathStates = new();
    private Repository? _currentRepo;

    /// <summary>
    /// Initializes a new <see cref="GitVersionControl"/>.
    /// </summary>
    public GitVersionControl(IShellHandler shellHandler) => _shellHandler = shellHandler;

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
        if (PathTools.PathsAreEqual(_currentRepo?.Info.Path, gitDir))
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
            ? PathTools.NormalizePath(_currentRepo.Info.WorkingDirectory)
            : null;

    public IResult DownloadChanges() =>
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
            string absolutePath = PathTools.NormalizePath(
                Path.Combine(_currentRepo.Info.WorkingDirectory, entry.FilePath)
            );
            VcStatus status = ConvertToVcStatus(entry.State);
            _pathStates[absolutePath] = status;

            if (status is VcStatus.Unstaged or VcStatus.Staged)
                SetDirStatuses(absolutePath, status);
        }
    }

    private void SetDirStatuses(string absolutePath, VcStatus status)
    {
        string? parentPath = absolutePath;
        while (
            (parentPath = Path.GetDirectoryName(parentPath))?.Length
            > _currentRepo!.Info.WorkingDirectory.Length
        )
            if (status is VcStatus.Unstaged)
                _pathStates[parentPath] = VcStatus.Unstaged;
            else if (
                !_pathStates.TryGetValue(parentPath, out VcStatus currentStatus)
                || currentStatus is not VcStatus.Unstaged
            )
                _pathStates[parentPath] = VcStatus.Staged;
    }

    private static VcStatus ConvertToVcStatus(FileStatus status)
    {
        if (status is FileStatus.Ignored or FileStatus.Nonexistent or FileStatus.Unaltered)
            return VcStatus.NotVersionControlled;

        if (
            status.HasFlag(FileStatus.NewInIndex)
            || status.HasFlag(FileStatus.ModifiedInIndex)
            || status.HasFlag(FileStatus.DeletedFromIndex)
            || status.HasFlag(FileStatus.RenamedInIndex)
            || status.HasFlag(FileStatus.TypeChangeInIndex)
        )
            return VcStatus.Staged;

        if (
            status.HasFlag(FileStatus.NewInWorkdir)
            || status.HasFlag(FileStatus.ModifiedInWorkdir)
            || status.HasFlag(FileStatus.DeletedFromWorkdir)
            || status.HasFlag(FileStatus.RenamedInWorkdir)
            || status.HasFlag(FileStatus.TypeChangeInWorkdir)
        )
            return VcStatus.Unstaged;

        return VcStatus.NotVersionControlled;
    }

    public VcStatus GetStatus(string filePath) =>
        _currentRepo is not null
            ? _pathStates.GetValueOrDefault(
                PathTools.NormalizePath(filePath),
                VcStatus.NotVersionControlled
            )
            : VcStatus.NotVersionControlled;

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

    public IResult UploadChanges() =>
        _currentRepo is null
            ? SimpleResult.Error(MissingRepoMessage)
            : _shellHandler.ExecuteCommand("git", "-C", GetWorkingDir()!, "push");

    /// <summary>
    /// Disposes of <see cref="_currentRepo"/>.
    /// </summary>
    public void Dispose() => _currentRepo?.Dispose();
}
