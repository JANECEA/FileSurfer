using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

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

public class BaseIconProvider : IIconProvider
{
    private static readonly Bitmap GenericFileIcon = new(
        AssetLoader.Open(new Uri("avares://FileSurfer.Core/Assets/GenericFileIcon.png"))
    );
    private static readonly Bitmap DirectoryIcon = new(
        AssetLoader.Open(new Uri("avares://FileSurfer.Core/Assets/FolderIcon.png"))
    );
    private static readonly Bitmap DriveIcon = new(
        AssetLoader.Open(new Uri("avares://FileSurfer.Core/Assets/DriveIcon.png"))
    );

    public virtual Task<Bitmap> GetFileIcon(string filePath) => Task.FromResult(GenericFileIcon);

    public virtual Task<Bitmap> GetDirectoryIcon(string dirPath) => Task.FromResult(DirectoryIcon);

    public virtual Task<Bitmap> GetDriveIcon(DriveEntry driveEntry) => Task.FromResult(DriveIcon);

    public virtual void Dispose() { }
}
