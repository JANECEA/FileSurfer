namespace FileSurfer.Models.Shell;

public interface IFileRestorer
{
    /// <summary>
    /// Restores a file based on <paramref name="originalFilePath"/>.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult RestoreFile(string originalFilePath);

    /// <summary>
    /// Restores a directory based on <paramref name="originalDirPath"/>.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult RestoreDir(string originalDirPath);
}