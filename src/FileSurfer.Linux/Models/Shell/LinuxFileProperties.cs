using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Shell;

namespace FileSurfer.Linux.Models.Shell;

public class LinuxFileProperties : IFileProperties
{
    // TODO use valued result
    // TODO implement simple properties dialog
    public IResult ShowFileProperties(IFileSystemEntry entry) =>
        throw new System.NotImplementedException();

    public bool SupportsOpenAs(IFileSystemEntry entry) => false;

    public IResult ShowOpenAsDialog(IFileSystemEntry entry) =>
        SimpleResult.Error("Unsupported operating system");
}
