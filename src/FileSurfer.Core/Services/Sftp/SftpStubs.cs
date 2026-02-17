using System;
using System.Collections.Generic;
using FileSurfer.Core.Models.FileOperations;
using FileSurfer.Core.Models.Shell;
using FileSurfer.Core.Models.VersionControl;
using FileSurfer.Core.ViewModels;

// Models that might be implemented for Sftp in the future

namespace FileSurfer.Core.Models.Sftp;

public class StubArchiveManager : IArchiveManager
{
    private readonly string _message;

    public StubArchiveManager(string message) => _message = message;

    public bool IsZipped(string filePath) => false;

    public IResult ZipFiles(
        IEnumerable<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName
    ) => SimpleResult.Error(_message);

    public IResult UnzipArchive(string archivePath, string destinationPath) =>
        SimpleResult.Error(_message);
}

public class StubGitIntegration : IGitIntegration
{
    private readonly string _message;

    public StubGitIntegration(string message) => _message = message;

    public bool InitIfGitRepository(string directoryPath) => false;

    public GitStatus GetStatus(string filePath) => GitStatus.NotVersionControlled;

    public IResult PullChanges() => SimpleResult.Error(_message);

    public string[] GetBranches() => Array.Empty<string>();

    public string GetCurrentBranchName() => string.Empty;

    public IResult SwitchBranches(string branchName) => SimpleResult.Error(_message);

    public IResult StagePath(string path) => SimpleResult.Error(_message);

    public IResult UnstagePath(string filePath) => SimpleResult.Error(_message);

    public IResult CommitChanges(string commitMessage) => SimpleResult.Error(_message);

    public IResult PushChanges() => SimpleResult.Error(_message);

    public void Dispose() { }
}

public class StubBinInteraction : IBinInteraction
{
    private readonly string _message;

    public StubBinInteraction(string message) => _message = message;

    public IResult MoveFileToTrash(string filePath) => SimpleResult.Error(_message);

    public IResult MoveDirToTrash(string dirPath) => SimpleResult.Error(_message);

    public IResult RestoreFile(string originalFilePath) => SimpleResult.Error(_message);

    public IResult RestoreDir(string originalDirPath) => SimpleResult.Error(_message);
}

public class StubFileProperties : IFileProperties
{
    private readonly string _message;

    public StubFileProperties(string message) => _message = message;

    public IResult ShowFileProperties(FileSystemEntryViewModel entry) =>
        SimpleResult.Error(_message);

    public bool SupportsOpenAs(IFileSystemEntry entry) => false;

    public IResult ShowOpenAsDialog(IFileSystemEntry entry) => SimpleResult.Error(_message);
}

public class StubShellHandler : IShellHandler
{
    private readonly string _message;

    public StubShellHandler(string message) => _message = message;

    public IResult CreateFileLink(string filePath) => SimpleResult.Error(_message);

    public IResult CreateDirectoryLink(string dirPath) => SimpleResult.Error(_message);

    public ValueResult<string> ExecuteCommand(string programName, params string[] args) =>
        ValueResult<string>.Error(_message);
}
