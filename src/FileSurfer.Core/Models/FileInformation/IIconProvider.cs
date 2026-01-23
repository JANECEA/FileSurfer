using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace FileSurfer.Core.Models.FileInformation;

/// <summary>
/// Optimizes icon delivery based on relevant criteria.
/// </summary>
public interface IIconProvider : IDisposable
{
    /// <summary>
    /// Retrieves the associated icon based on the supplied file path.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns>The associated icon.</returns>
    public Task<Bitmap> GetFileIcon(string filePath);

    /// <summary>
    /// Retrieves the icon associated with directories.
    /// </summary>
    public Task<Bitmap> GetDirectoryIcon(string dirPath);

    /// <summary>
    /// Retrieves the icon associated with drives.
    /// </summary>
    public Task<Bitmap> GetDriveIcon(DriveEntry driveEntry);
}
