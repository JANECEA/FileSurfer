namespace FileSurfer.Models.Shell;

public interface IFileRestorer
{
    /// <summary>
    /// Restores a file based on <paramref name="originalFilePath"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
    public IFileOperationResult RestoreFile(string originalFilePath);

    /// <summary>
    /// Restores a directory based on <paramref name="originalDirPath"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
    public IFileOperationResult RestoreDir(string originalDirPath);
}