using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;

namespace Mocks;

public class MockFileIoHandler : IFileIoHandler
{
    public virtual IResult NewFileAt(string dirPath, string fileName) =>
        throw new NotImplementedException();

    public virtual IResult NewDirAt(string dirPath, string dirName) =>
        throw new NotImplementedException();

    public virtual IResult RenameFileAt(string filePath, string newName) =>
        throw new NotImplementedException();

    public virtual IResult RenameDirAt(string dirPath, string newName) =>
        throw new NotImplementedException();

    public virtual IResult MoveFileTo(string filePath, string destinationDir) =>
        throw new NotImplementedException();

    public virtual IResult MoveDirTo(string dirPath, string destinationDir) =>
        throw new NotImplementedException();

    public virtual IResult CopyFileTo(string filePath, string destinationDir) =>
        throw new NotImplementedException();

    public virtual IResult CopyDirTo(string dirPath, string destinationDir) =>
        throw new NotImplementedException();

    public virtual IResult DuplicateFile(string filePath, string copyName) =>
        throw new NotImplementedException();

    public virtual IResult DuplicateDir(string dirPath, string copyName) =>
        throw new NotImplementedException();

    public virtual IResult DeleteFile(string filePath) => throw new NotImplementedException();

    public virtual IResult DeleteDir(string dirPath) => throw new NotImplementedException();

    public virtual Task<IResult> WriteFileStreamAsync(
        FileTransferStream fileStream,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    ) => throw new NotImplementedException();

    public virtual Task<IResult> WriteDirStreamAsync(
        DirTransferStream dirStream,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    ) => throw new NotImplementedException();
}
