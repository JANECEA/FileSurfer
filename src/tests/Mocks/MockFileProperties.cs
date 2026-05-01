using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.ViewModels;

namespace Mocks;

public class MockFileProperties : IFileProperties
{
    public virtual IResult ShowFileProperties(FileSystemEntryViewModel entry) =>
        throw new NotImplementedException();

    public virtual bool SupportsOpenAs(IFileSystemEntry entry) =>
        throw new NotImplementedException();

    public virtual IResult ShowOpenAsDialog(IFileSystemEntry entry) =>
        throw new NotImplementedException();
}
