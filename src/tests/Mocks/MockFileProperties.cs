using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.ViewModels;

namespace Mocks;

public class MockFileProperties : ServiceMock, IFileProperties
{
    public virtual IResult ShowFileProperties(FileSystemEntryViewModel entry)
    {
        RecordCall(nameof(ShowFileProperties), entry);
        return SimpleResult.Ok();
    }

    public virtual bool SupportsOpenAs(IFileSystemEntry entry)
    {
        RecordCall(nameof(SupportsOpenAs), entry);
        return true;
    }

    public virtual IResult ShowOpenAsDialog(IFileSystemEntry entry)
    {
        RecordCall(nameof(ShowOpenAsDialog), entry);
        return SimpleResult.Ok();
    }
}
