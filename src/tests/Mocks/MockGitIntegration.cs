using FileSurfer.Core.Models;
using FileSurfer.Core.Services.VersionControl;

namespace Mocks;

public class MockGitIntegration : IGitIntegration
{
    public void Dispose() => throw new NotImplementedException();

    public bool InitIfGitRepository(string directoryPath) => throw new NotImplementedException();

    public GitStatus GetStatus(string filePath) => throw new NotImplementedException();

    public Task<IResult> FetchChangesAsync() => throw new NotImplementedException();

    public Task<ValueResult<string>> PullChangesAsync() => throw new NotImplementedException();

    public RepoDetails? GetRepositoryState() => throw new NotImplementedException();

    public string[] GetBranches() => throw new NotImplementedException();

    public string GetCurrentBranchName() => throw new NotImplementedException();

    public IResult SwitchBranches(string branchName) => throw new NotImplementedException();

    public IResult StagePath(string path) => throw new NotImplementedException();

    public IResult UnstagePath(string path) => throw new NotImplementedException();

    public IResult StashChanges() => throw new NotImplementedException();

    public IResult PopChanges() => throw new NotImplementedException();

    public IResult RestorePath(string path) => throw new NotImplementedException();

    public Task<ValueResult<string>> CommitChangesAsync(string commitMessage) =>
        throw new NotImplementedException();

    public Task<ValueResult<string>> PushChangesAsync() => throw new NotImplementedException();
}
