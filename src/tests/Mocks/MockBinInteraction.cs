using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;

namespace Mocks;

public class MockBinInteraction : IBinInteraction
{
    public IResult MoveFileToTrash(string filePath) => throw new NotImplementedException();

    public IResult MoveDirToTrash(string dirPath) => throw new NotImplementedException();

    public IResult RestoreFile(string originalFilePath) => throw new NotImplementedException();

    public IResult RestoreDir(string originalDirPath) => throw new NotImplementedException();

    public void Dispose() { }
}
