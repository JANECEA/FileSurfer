using Avalonia.Media.Imaging;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace Mocks;

public class MockIconProvider : IIconProvider
{
    public Task<Bitmap> GetFileIconAsync(string filePath) => throw new NotImplementedException();

    public Task<Bitmap> GetDirectoryIconAsync(string dirPath) =>
        throw new NotImplementedException();

    public Task<Bitmap> GetDriveIconAsync(DriveEntryInfo driveEntryInfo) =>
        throw new NotImplementedException();

    public void Dispose() => throw new NotImplementedException();
}
