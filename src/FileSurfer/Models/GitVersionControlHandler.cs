using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace FileSurfer;

public class GitVersionControlHandler : IVersionControl
{
    private Repository? _currentRepo = null;

    public bool IsVersionControlled(string directoryPath)
    {
        _currentRepo?.Dispose();
        string? repoPath = directoryPath;
        while (repoPath is not null)
        {
            string gitDir = Path.Combine(repoPath, ".git");
            if (!Directory.Exists(gitDir))
            {
                repoPath = Directory.GetParent(repoPath)?.FullName;
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
        string command = $"cd \"{_currentRepo.Info.Path}\" && git pull";
        return ExecuteCmd(command, out errorMessage);
    }

    public string GetCurrentBranchName() => _currentRepo.Head.FriendlyName;

    public string[] GetBranches() =>
        _currentRepo.Branches.Where(b => !b.IsRemote).Select(b => b.FriendlyName).ToArray();

    public bool SwitchBranches(string branchName, out string? errorMessage)
    {
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
        string command = $"cd \"{_currentRepo.Info.Path}\" && git commit -m \"{commitMessage}\"";
        return ExecuteCmd(command, out errorMessage);
    }

    public bool UploadChanges(out string? errorMessage)
    {
        string command = $"cd \"{_currentRepo.Info.Path}\" && git push";
        return ExecuteCmd(command, out errorMessage);
    }

    private bool ExecuteCmd(string command, out string? errorMessage)
    {
        using Process process =
            new()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            errorMessage = null;
            return true;
        }
        errorMessage = process.StandardError.ReadToEnd();
        return false;
    }
}
