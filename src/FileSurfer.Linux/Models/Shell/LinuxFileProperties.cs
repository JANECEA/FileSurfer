using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Shell;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Linux.Models.Shell;

public interface IPropertiesVmFactory
{
    public ValueResult<IDisplayable> GetPropertiesVm(FileSystemEntryViewModel entry);
}

public interface IDisplayable
{
    public void Show();
}

public class LinuxFileProperties : IFileProperties
{
    private readonly IPropertiesVmFactory _vmFactory;

    public LinuxFileProperties(IPropertiesVmFactory vmFactory) => _vmFactory = vmFactory;

    public IResult ShowFileProperties(FileSystemEntryViewModel entry)
    {
        ValueResult<IDisplayable> result = _vmFactory.GetPropertiesVm(entry);
        if (!result.IsOk)
            return result;

        result.Value.Show();
        return SimpleResult.Ok();
    }

    public bool SupportsOpenAs(IFileSystemEntry entry) => false;

    public IResult ShowOpenAsDialog(IFileSystemEntry entry) =>
        SimpleResult.Error("Unsupported operating system");
}
