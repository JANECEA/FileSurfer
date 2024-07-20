namespace FileSurfer;

public interface IVersionControl
{
    public bool IsVersionControlled(string directoryPath);

    public bool TryDownload(out string? errorMessage);
    
    public string[] GetBranches(out string? errorMessage);

    public bool StageChange(string filePath, out string? errorMessage);

    public bool CommitChanges(string commitMessage, out string? errorMessage);

    public bool UploadChanges(out string? errorMessage);
}