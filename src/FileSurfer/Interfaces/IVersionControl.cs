namespace FileSurfer.Models;

public interface IVersionControl
{
    public bool IsVersionControlled(string directoryPath);

    public VCStatus ConsolidateStatus(string path);

    public bool DownloadChanges(out string? errorMessage);

    public string[] GetBranches();

    public string GetCurrentBranchName();

    public bool SwitchBranches(string branchName, out string? errorMessage);

    public bool StageChange(string filePath, out string? errorMessage);

    public bool UnstageChange(string filePath, out string? errorMessage);

    public bool CommitChanges(string commitMessage, out string? errorMessage);

    public bool UploadChanges(out string? errorMessage);
}
