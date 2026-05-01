using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;

namespace Mocks;

public class MockBinInteraction : IBinInteraction
{
    public virtual IResult MoveFileToTrash(string filePath) => throw new NotImplementedException();

    public virtual IResult MoveDirToTrash(string dirPath) => throw new NotImplementedException();

    public virtual IResult RestoreFile(string originalFilePath) =>
        throw new NotImplementedException();

    public virtual IResult RestoreDir(string originalDirPath) =>
        throw new NotImplementedException();

    public virtual void Dispose() { }
}
