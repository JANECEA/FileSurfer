namespace FileSurfer.Models.Shell;

public interface IFileRestorer
{
    /// <summary>
    /// Restores a file based on <paramref name="originalFilePath"/>.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult RestoreFile(string originalFilePath);

    /// <summary>
    /// Restores a directory based on <paramref name="originalDirPath"/>.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult RestoreDir(string originalDirPath);
}
