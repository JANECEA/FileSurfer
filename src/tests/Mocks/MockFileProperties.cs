using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.ViewModels;

namespace Mocks;

public class MockFileProperties : IFileProperties
{
    public IResult ShowFileProperties(FileSystemEntryViewModel entry) =>
        throw new NotImplementedException();

    public bool SupportsOpenAs(IFileSystemEntry entry) => throw new NotImplementedException();

    public IResult ShowOpenAsDialog(IFileSystemEntry entry) => throw new NotImplementedException();
}
