using Avalonia.Media.Imaging;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace Mocks;

public class MockIconProvider : IIconProvider
{
    public virtual Task<Bitmap> GetFileIconAsync(string filePath) =>
        throw new NotImplementedException();

    public virtual Task<Bitmap> GetDirectoryIconAsync(string dirPath) =>
        throw new NotImplementedException();

    public virtual Task<Bitmap> GetDriveIconAsync(DriveEntryInfo driveEntryInfo) =>
        throw new NotImplementedException();

    public virtual void Dispose() => throw new NotImplementedException();
}
