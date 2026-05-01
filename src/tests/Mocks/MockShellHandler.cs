using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;

namespace Mocks;

public class MockShellHandler : ServiceMock, IShellHandler
{
    public virtual IResult CreateFileLink(string filePath)
    {
        RecordCall(nameof(CreateFileLink), filePath);
        return SimpleResult.Ok();
    }

    public virtual IResult CreateDirectoryLink(string dirPath)
    {
        RecordCall(nameof(CreateDirectoryLink), dirPath);
        return SimpleResult.Ok();
    }

    public virtual ValueResult<string> ExecuteCommand(string programName, params string[] args)
    {
        RecordCall(nameof(ExecuteCommand), programName, args);
        return ValueResult<string>.Ok(string.Empty);
    }

    public virtual Task<ValueResult<string>> ExecuteCommandAsync(
        string programName,
        params string[] args
    )
    {
        RecordCall(nameof(ExecuteCommandAsync), programName, args);
        return Task.FromResult(ValueResult<string>.Ok(string.Empty));
    }
}
