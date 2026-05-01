using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;

namespace Mocks;

public class MockShellHandler : IShellHandler
{
    public IResult CreateFileLink(string filePath) => throw new NotImplementedException();

    public IResult CreateDirectoryLink(string dirPath) => throw new NotImplementedException();

    public ValueResult<string> ExecuteCommand(string programName, params string[] args) =>
        throw new NotImplementedException();

    public Task<ValueResult<string>> ExecuteCommandAsync(
        string programName,
        params string[] args
    ) => throw new NotImplementedException();
}
