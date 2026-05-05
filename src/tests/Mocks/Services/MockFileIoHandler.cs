using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;

namespace Mocks.Services;

public class MockFileIoHandler : ServiceMock, IFileIoHandler
{
    public virtual IResult NewFileAt(string dirPath, string fileName)
    {
        RecordCall(nameof(NewFileAt), dirPath, fileName);
        return SimpleResult.Ok();
    }

    public virtual IResult NewDirAt(string dirPath, string dirName)
    {
        RecordCall(nameof(NewDirAt), dirPath, dirName);
        return SimpleResult.Ok();
    }

    public virtual IResult RenameFileAt(string filePath, string newName)
    {
        RecordCall(nameof(RenameFileAt), filePath, newName);
        return SimpleResult.Ok();
    }

    public virtual IResult RenameDirAt(string dirPath, string newName)
    {
        RecordCall(nameof(RenameDirAt), dirPath, newName);
        return SimpleResult.Ok();
    }

    public virtual IResult MoveFileTo(string filePath, string destinationDir)
    {
        RecordCall(nameof(MoveFileTo), filePath, destinationDir);
        return SimpleResult.Ok();
    }

    public virtual IResult MoveDirTo(string dirPath, string destinationDir)
    {
        RecordCall(nameof(MoveDirTo), dirPath, destinationDir);
        return SimpleResult.Ok();
    }

    public virtual IResult CopyFileTo(string filePath, string destinationDir)
    {
        RecordCall(nameof(CopyFileTo), filePath, destinationDir);
        return SimpleResult.Ok();
    }

    public virtual IResult CopyDirTo(string dirPath, string destinationDir)
    {
        RecordCall(nameof(CopyDirTo), dirPath, destinationDir);
        return SimpleResult.Ok();
    }

    public virtual IResult DuplicateFile(string filePath, string copyName)
    {
        RecordCall(nameof(DuplicateFile), filePath, copyName);
        return SimpleResult.Ok();
    }

    public virtual IResult DuplicateDir(string dirPath, string copyName)
    {
        RecordCall(nameof(DuplicateDir), dirPath, copyName);
        return SimpleResult.Ok();
    }

    public virtual IResult DeleteFile(string filePath)
    {
        RecordCall(nameof(DeleteFile), filePath);
        return SimpleResult.Ok();
    }

    public virtual IResult DeleteDir(string dirPath)
    {
        RecordCall(nameof(DeleteDir), dirPath);
        return SimpleResult.Ok();
    }

    public virtual Task<IResult> WriteFileStreamAsync(
        FileTransferStream fileStream,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        RecordCall(nameof(WriteFileStreamAsync), fileStream, dirPath, reporter, ct);
        return Task.FromResult<IResult>(SimpleResult.Ok());
    }

    public virtual Task<IResult> WriteDirStreamAsync(
        DirTransferStream dirStream,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        RecordCall(nameof(WriteDirStreamAsync), dirStream, dirPath, reporter, ct);
        return Task.FromResult<IResult>(SimpleResult.Ok());
    }
}
