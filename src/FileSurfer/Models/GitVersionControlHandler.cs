using System;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace FileSurfer;

public class GitVersionControlHandler : IVersionControl, IDisposable
{
    private const string MissingRepoMessage = "No git repository found";
    private readonly IFileOperationsHandler _fileOpsHandler;
    private Repository? _currentRepo = null;

    public GitVersionControlHandler(IFileOperationsHandler _fileOperationsHandler) =>
        _fileOpsHandler = _fileOperationsHandler;

    public bool IsVersionControlled(string directoryPath)
    {
        _currentRepo?.Dispose();
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
                _currentRepo = new Repository(gitDir);
                return true;
            }
            catch
            {
                break;
            }
        }
        _currentRepo = null;
        return false;
    }

    public string[] GetUnstagedFiles() =>
        _currentRepo
            .RetrieveStatus()
            .Where(entry => entry.State.HasFlag(FileStatus.ModifiedInWorkdir))
            .Select(entry => entry.FilePath)
            .ToArray();

    public bool DownloadChanges(out string? errorMessage)
    {
        if (_currentRepo is not null)
        {
            string command = $"cd \"{_currentRepo.Info.Path}\\..\" && git pull";
            errorMessage = null;
            return _fileOpsHandler.ExecuteCmd(command);
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

    public bool StageChange(string filePath, out string? errorMessage)
    {
        try
        {
            if (_currentRepo is null)
            {
                errorMessage = MissingRepoMessage;
                return false;
            }
            _currentRepo.Index.Add(filePath);
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

    public bool CommitChanges(string commitMessage, out string? errorMessage)
    {
        if (_currentRepo is null)
        {
            errorMessage = MissingRepoMessage;
            return false;
        }
        string command = $"cd \"{_currentRepo.Info.Path}\\..\" && git commit -m \"{commitMessage}\"";
        errorMessage = null;
        return _fileOpsHandler.ExecuteCmd(command);
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
        return _fileOpsHandler.ExecuteCmd(command);
    }

    public void Dispose() => _currentRepo?.Dispose();
}
