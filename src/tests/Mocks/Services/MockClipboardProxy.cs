using Avalonia.Media.Imaging;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.FileOperations;

namespace Mocks.Services;

public class MockClipboardProxy : ServiceMock, IOsClipboardProxy
{
    public virtual Task<IResult> SetTextAsync(string text)
    {
        RecordCall(nameof(SetTextAsync), text);
        return Task.FromResult<IResult>(SimpleResult.Ok());
    }

    public virtual Task<Bitmap?> TryGetBitmapAsync()
    {
        RecordCall(nameof(TryGetBitmapAsync));
        return Task.FromResult<Bitmap?>(null);
    }

    public virtual Task<string?> TryGetTextAsync()
    {
        RecordCall(nameof(TryGetTextAsync));
        return Task.FromResult<string?>(null);
    }

    public virtual Task<IFileSystemEntry[]?> TryGetFilesAsync()
    {
        RecordCall(nameof(TryGetFilesAsync));
        return Task.FromResult<IFileSystemEntry[]?>(null);
    }

    public virtual Task<IResult> ClearAsync()
    {
        RecordCall(nameof(ClearAsync));
        return Task.FromResult<IResult>(SimpleResult.Ok());
    }

    public virtual Task<IResult> CopyToOsClipboardAsync(IFileSystemEntry[] entries)
    {
        RecordCall(nameof(CopyToOsClipboardAsync), (object)entries);
        return Task.FromResult<IResult>(SimpleResult.Ok());
    }
}
