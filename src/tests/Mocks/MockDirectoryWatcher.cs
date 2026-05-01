using FileSurfer.Core.Models;
using FileSurfer.Core.Services.FileInformation;

namespace Mocks;

public class MockDirectoryWatcher : IDirectoryWatcher
{
    private readonly IReadOnlyList<FileSystemEvent> _fileSystemEvents;

    public event Func<FileSystemEvent, Task>? ChangeDetected;

    public MockDirectoryWatcher(IReadOnlyList<FileSystemEvent> fileSystemEvents) =>
        _fileSystemEvents = fileSystemEvents;

    public async Task<IResult> StartAsync(
        TimeSpan pollingInterval,
        bool syncHidden,
        CancellationToken token
    )
    {
        if (ChangeDetected is null)
            return SimpleResult.Error("No event handler set.");

        foreach (FileSystemEvent fsEvent in _fileSystemEvents)
            await ChangeDetected.Invoke(fsEvent);

        return SimpleResult.Ok();
    }
}
