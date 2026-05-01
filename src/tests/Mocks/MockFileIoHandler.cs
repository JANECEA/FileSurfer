using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;

namespace Mocks;

public class MockFileIoHandler : IFileIoHandler
{
    public IResult NewFileAt(string dirPath, string fileName) =>
        throw new NotImplementedException();

    public IResult NewDirAt(string dirPath, string dirName) => throw new NotImplementedException();

    public IResult RenameFileAt(string filePath, string newName) =>
        throw new NotImplementedException();

    public IResult RenameDirAt(string dirPath, string newName) =>
        throw new NotImplementedException();

    public IResult MoveFileTo(string filePath, string destinationDir) =>
        throw new NotImplementedException();

    public IResult MoveDirTo(string dirPath, string destinationDir) =>
        throw new NotImplementedException();

    public IResult CopyFileTo(string filePath, string destinationDir) =>
        throw new NotImplementedException();

    public IResult CopyDirTo(string dirPath, string destinationDir) =>
        throw new NotImplementedException();

    public IResult DuplicateFile(string filePath, string copyName) =>
        throw new NotImplementedException();

    public IResult DuplicateDir(string dirPath, string copyName) =>
        throw new NotImplementedException();

    public IResult DeleteFile(string filePath) => throw new NotImplementedException();

    public IResult DeleteDir(string dirPath) => throw new NotImplementedException();

    public Task<IResult> WriteFileStreamAsync(
        FileTransferStream fileStream,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    ) => throw new NotImplementedException();

    public Task<IResult> WriteDirStreamAsync(
        DirTransferStream dirStream,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    ) => throw new NotImplementedException();
}
