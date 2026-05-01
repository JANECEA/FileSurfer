using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;

namespace Mocks;

public class MockShellHandler : IShellHandler
{
    public virtual IResult CreateFileLink(string filePath) => throw new NotImplementedException();

    public virtual IResult CreateDirectoryLink(string dirPath) =>
        throw new NotImplementedException();

    public virtual ValueResult<string> ExecuteCommand(string programName, params string[] args) =>
        throw new NotImplementedException();

    public virtual Task<ValueResult<string>> ExecuteCommandAsync(
        string programName,
        params string[] args
    ) => throw new NotImplementedException();
}
