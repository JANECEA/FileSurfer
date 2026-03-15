using System;
using System.Collections.Generic;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.Services.VersionControl;
using FileSurfer.Core.ViewModels;

// Models that might be implemented for Sftp in the future

namespace FileSurfer.Core.Services.Sftp;

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
    private readonly ValueResult<string> _result;

    public StubGitIntegration(string message) => _result = ValueResult<string>.Error(message);

    public void Dispose() { }

    public bool InitIfGitRepository(string directoryPath) => false;

    public GitStatus GetStatus(string filePath) => GitStatus.NotVersionControlled;

    public IResult FetchChanges() => _result;

    public ValueResult<string> PullChanges() => _result;

    public RepoDetails? GetRepositoryState() => null;

    public string[] GetBranches() => [];

    public string GetCurrentBranchName() => string.Empty;

    public IResult SwitchBranches(string branchName) => _result;

    public IResult StagePath(string path) => _result;

    public IResult UnstagePath(string filePath) => _result;

    public IResult CommitChanges(string commitMessage) => _result;

    public ValueResult<string> PushChanges() => _result;
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
