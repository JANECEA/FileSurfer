namespace FileSurfer.Models.Shell;

public interface IFileRestorer
{
    /// <summary>
    /// Moves a file to the trash.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult MoveFileToTrash(string filePath);

    /// <summary>
    /// Moves a directory to the trash.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult MoveDirToTrash(string dirPath);

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
