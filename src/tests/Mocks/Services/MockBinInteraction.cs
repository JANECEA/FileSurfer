using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;

namespace Mocks.Services;

public class MockBinInteraction : ServiceMock, IBinInteraction
{
    public virtual IResult MoveFileToTrash(string filePath)
    {
        RecordCall(nameof(MoveFileToTrash), filePath);
        return SimpleResult.Ok();
    }

    public virtual IResult MoveDirToTrash(string dirPath)
    {
        RecordCall(nameof(MoveDirToTrash), dirPath);
        return SimpleResult.Ok();
    }

    public virtual IResult RestoreFile(string originalFilePath)
    {
        RecordCall(nameof(RestoreFile), originalFilePath);
        return SimpleResult.Ok();
    }

    public virtual IResult RestoreDir(string originalDirPath)
    {
        RecordCall(nameof(RestoreDir), originalDirPath);
        return SimpleResult.Ok();
    }

    public virtual void Dispose() => RecordCall(nameof(Dispose));
}
