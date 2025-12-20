using Avalonia.Media;

namespace FileSurfer.Core.Models.FileInformation;

/// <summary>
/// Optimizes icon delivery based on relevant criteria.
/// </summary>
public interface IIconProvider
{
    /// <summary>
    /// Retrieves the associated icon based on the supplied file path.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns>The associated icon if file exists, otherwise returns <see langword="null" />.</returns>
    public IImage? GetFileIcon(string filePath);

    /// <summary>
    /// Retrieves the icon associated with directories.
    /// </summary>
    public IImage GetDirectoryIcon(string dirPath);

    /// <summary>
    /// Retrieves the icon associated with drives.
    /// </summary>
    public IImage GetDriveIcon(DriveEntry driveEntry);
}
