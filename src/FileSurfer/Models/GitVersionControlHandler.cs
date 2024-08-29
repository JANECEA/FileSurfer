using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace FileSurfer.Models;

public enum VCStatus
{
    NotVersionControlled,
    Staged,
    Unstaged,
}

public class GitVersionControlHandler : IVersionControl, IDisposable
{
    private const string MissingRepoMessage = "No git repository found";
    private readonly IFileIOHandler _fileIOHandler;
    private Repository? _currentRepo = null;
    private readonly Dictionary<string, VCStatus> _statusDict = new();

    public GitVersionControlHandler(IFileIOHandler fileIOHandler) =>
        _fileIOHandler = fileIOHandler;

    public bool IsVersionControlled(string directoryPath)
    {
        string? repoPath = directoryPath;
        while (repoPath is not null)
        {
            string gitDir = Path.Combine(repoPath, ".git");
            if (!Directory.Exists(gitDir))
            {
                repoPath = Path.GetDirectoryName(repoPath);
                continue;
            }
            try
            {
                if (_currentRepo?.Info.Path != gitDir)
                {
                    _currentRepo?.Dispose();
                    _currentRepo = new Repository(gitDir);
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
        return false;
    }

    public bool DownloadChanges(out string? errorMessage)
    {
        if (_currentRepo is not null)
        {
            string command = $"cd \"{_currentRepo.Info.Path}\\..\" && git pull";
            errorMessage = null;
            return _fileIOHandler.ExecuteCmd(command);
        }
        errorMessage = MissingRepoMessage;
        return false;
    }

    public string GetCurrentBranchName() =>
        _currentRepo is null ? string.Empty : _currentRepo.Head.FriendlyName;

    public string[] GetBranches() =>
        _currentRepo is null
            ? Array.Empty<string>()
            : _currentRepo.Branches.Where(b => !b.IsRemote).Select(b => b.FriendlyName).ToArray();

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
            errorMessage = $"branch: {branchName} not found";
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
            }
        );
        _statusDict.Clear();
        foreach (StatusEntry? entry in repoStatus)
        {
            string absolutePath = Path.Combine(_currentRepo.Info.WorkingDirectory, entry.FilePath)
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

    public VCStatus ConsolidateStatus(string path)
    {
        if (_currentRepo is null)
            return VCStatus.NotVersionControlled;

        return _statusDict.TryGetValue(path.Replace('\\', '/'), out VCStatus status)
            ? status
            : VCStatus.NotVersionControlled;
    }

    public bool StageChange(string filePath, out string? errorMessage)
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

    public bool UnstageChange(string filePath, out string? errorMessage)
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

    public bool CommitChanges(string commitMessage, out string? errorMessage)
    {
        if (_currentRepo is null)
        {
            errorMessage = MissingRepoMessage;
            return false;
        }
        string command =
            $"cd \"{_currentRepo.Info.Path}\\..\" && git commit -m \"{commitMessage}\"";
        errorMessage = null;
        return _fileIOHandler.ExecuteCmd(command);
    }

    public bool UploadChanges(out string? errorMessage)
    {
        if (_currentRepo is null)
        {
            errorMessage = MissingRepoMessage;
            return false;
        }
        string command = $"cd \"{_currentRepo.Info.Path}\\..\" && git push";
        errorMessage = null;
        return _fileIOHandler.ExecuteCmd(command);
    }

    public void Dispose()
    {
        _currentRepo?.Dispose();
        GC.SuppressFinalize(this);
    }
}
