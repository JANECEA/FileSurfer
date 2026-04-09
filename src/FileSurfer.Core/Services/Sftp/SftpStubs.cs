using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.Services.VersionControl;

// Services that might be implemented for Sftp in the future

namespace FileSurfer.Core.Services.Sftp;

public class StubArchiveManager : IArchiveManager
{
    private readonly string _message;

    public StubArchiveManager(string message) => _message = message;

    public bool IsArchived(string filePath) => false;

    public Task<IResult> ArchiveEntriesAsync(
        IList<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName,
        ProgressReporter reporter,
        CancellationToken ct
    ) => Task.FromResult<IResult>(SimpleResult.Error(_message));

    public Task<IResult> ExtractArchiveAsync(
        string archivePath,
        string destinationPath,
        ProgressReporter reporter,
        CancellationToken ct
    ) => Task.FromResult<IResult>(SimpleResult.Error(_message));
}

public class StubGitIntegration : IGitIntegration
{
    private readonly ValueResult<string> _result;
    private readonly Task<ValueResult<string>> _taskResult;

    public StubGitIntegration(string message) => _result = ValueResult<string>.Error(message);

    public void Dispose() { }

    public bool InitIfGitRepository(string directoryPath) => false;

    public GitStatus GetStatus(string filePath) => GitStatus.NotVersionControlled;

    public Task<IResult> FetchChangesAsync() => Task.FromResult<IResult>(_result);

    public Task<ValueResult<string>> PullChangesAsync() => Task.FromResult(_result);

    public RepoDetails? GetRepositoryState() => null;

    public string[] GetBranches() => [];

    public string GetCurrentBranchName() => string.Empty;

    public IResult SwitchBranches(string branchName) => _result;

    public IResult StagePath(string path) => _result;

    public IResult UnstagePath(string path) => _result;

    public IResult StashChanges() => _result;

    public IResult PopChanges() => _result;

    public IResult RestorePath(string path) => _result;

    public Task<ValueResult<string>> CommitChangesAsync(string commitMessage) =>
        Task.FromResult(_result);

    public Task<ValueResult<string>> PushChangesAsync() => Task.FromResult(_result);
}

public class StubBinInteraction : IBinInteraction
{
    private readonly string _message;

    public StubBinInteraction(string message) => _message = message;

    public IResult MoveFileToTrash(string filePath) => SimpleResult.Error(_message);

    public IResult MoveDirToTrash(string dirPath) => SimpleResult.Error(_message);

    public IResult RestoreFile(string originalFilePath) => SimpleResult.Error(_message);

    public IResult RestoreDir(string originalDirPath) => SimpleResult.Error(_message);

    public void Dispose() { }
}
