using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace Mocks.Models;

public class MockIconProvider : ServiceMock, IIconProvider
{
    private static readonly Bitmap MockBitmap = new WriteableBitmap(
        new PixelSize(1, 1),
        new Vector(96, 96),
        PixelFormat.Bgra8888,
        AlphaFormat.Premul
    );

    public virtual Task<Bitmap> GetFileIconAsync(string filePath)
    {
        RecordCall(nameof(GetFileIconAsync), filePath);
        return Task.FromResult(MockBitmap);
    }

    public virtual Task<Bitmap> GetDirectoryIconAsync(string dirPath)
    {
        RecordCall(nameof(GetDirectoryIconAsync), dirPath);
        return Task.FromResult(MockBitmap);
    }

    public virtual Task<Bitmap> GetDriveIconAsync(DriveEntryInfo driveEntryInfo)
    {
        RecordCall(nameof(GetDriveIconAsync), driveEntryInfo);
        return Task.FromResult(MockBitmap);
    }

    public virtual void Dispose() => RecordCall(nameof(Dispose));
}
