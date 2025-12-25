using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Shell;

namespace FileSurfer.Linux.Models.Shell;

public class LinuxFileProperties : IFileProperties
{
    // TODO use valued result
    // TODO implement simple properties dialog
    public IResult ShowFileProperties(string filePath)
    {
        return SimpleResult.Error("Unsupported operating system.");
    }

    // TODO hide the option on linux
    public IResult ShowOpenAsDialog(string filePath) =>
        SimpleResult.Error("Unsupported operating system.");
}
