using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileSurfer.Models.Shell;
using LibGit2Sharp;

namespace FileSurfer.Models.VersionControl;

/// <summary>
/// Handles git integration within the <see cref="FileSurfer"/> app.
/// </summary>
public class GitVersionControl : IVersionControl
{
    private const string MissingRepoMessage = "No git repository found";

    private readonly IShellHandler _shellHandler;

    private readonly Dictionary<string, VCStatus> _pathStates = new();
    private Repository? _currentRepo;

    /// <summary>
    /// Initializes a new <see cref="GitVersionControl"/>.
    /// </summary>
    public GitVersionControl(IShellHandler shellHandler) => _shellHandler = shellHandler;

    public IResult InitIfVersionControlled(string directoryPath)
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

        if (_currentRepo?.Info.Path == gitDir + '\\')
        {
            SetFileStates();
            return SimpleResult.Ok();
        }

        _currentRepo?.Dispose();
        if (repoRootDir is not null && Directory.Exists(repoRootDir))
        {
            try
            {
                _currentRepo = new Repository(repoRootDir);
                SetFileStates();
                return SimpleResult.Ok();
            }
            catch { }
        }
        _currentRepo = null;
        return SimpleResult.Error();
    }

    private string? GetWorkingDir() => _currentRepo?.Info.WorkingDirectory.TrimEnd('\\');

    public IResult DownloadChanges()
    {
        if (_currentRepo is null)
            return SimpleResult.Error(MissingRepoMessage);

        string command = $"git -C \"{GetWorkingDir()}\" pull";
        return _shellHandler.ExecuteCmd(command);
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
            return SimpleResult.Error(MissingRepoMessage);

        try
        {
            Branch branch = _currentRepo.Branches[branchName];
            Commands.Checkout(_currentRepo, branch);
            return SimpleResult.Ok();
        }
        catch
        {
            return SimpleResult.Error($"branch: \"{branchName}\" not found");
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
            string absolutePath = Path.Combine(_currentRepo.Info.WorkingDirectory, entry.FilePath)
                .Replace('/', '\\');
            VCStatus status = ConvertToVCStatus(entry.State);
            _pathStates[absolutePath] = status;

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
                _pathStates[parentPath] = VCStatus.Unstaged;
            else if (
                !_pathStates.TryGetValue(parentPath, out VCStatus currentStatus)
                || currentStatus is not VCStatus.Unstaged
            )
                _pathStates[parentPath] = VCStatus.Staged;
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

    public VCStatus GetStatus(string filePath) =>
        _currentRepo is not null
            ? _pathStates.GetValueOrDefault(filePath, VCStatus.NotVersionControlled)
            : VCStatus.NotVersionControlled;

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

            string relativePath = Path.GetRelativePath(
                _currentRepo.Info.WorkingDirectory,
                filePath
            );
            Commands.Unstage(_currentRepo, relativePath);
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

        string command = $"git -C \"{GetWorkingDir()}\" commit -m \"{commitMessage}\"";
        return _shellHandler.ExecuteCmd(command);
    }

    public IResult UploadChanges()
    {
        if (_currentRepo is null)
            return SimpleResult.Error(MissingRepoMessage);

        string command = $"git -C \"{GetWorkingDir()}\" push";
        return _shellHandler.ExecuteCmd(command);
    }

    /// <summary>
    /// Disposes of <see cref="_currentRepo"/>.
    /// </summary>
    public void Dispose() => _currentRepo?.Dispose();
}
