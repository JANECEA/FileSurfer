using FileSurfer.Core.Models;
using FileSurfer.Core.Services.VersionControl;

namespace Mocks;

public class MockGitIntegration : IGitIntegration
{
    public virtual void Dispose() => throw new NotImplementedException();

    public virtual bool InitIfGitRepository(string directoryPath) =>
        throw new NotImplementedException();

    public virtual GitStatus GetStatus(string filePath) => throw new NotImplementedException();

    public virtual Task<IResult> FetchChangesAsync() => throw new NotImplementedException();

    public virtual Task<ValueResult<string>> PullChangesAsync() =>
        throw new NotImplementedException();

    public virtual RepoDetails? GetRepositoryState() => throw new NotImplementedException();

    public virtual string[] GetBranches() => throw new NotImplementedException();

    public virtual string GetCurrentBranchName() => throw new NotImplementedException();

    public virtual IResult SwitchBranches(string branchName) => throw new NotImplementedException();

    public virtual IResult StagePath(string path) => throw new NotImplementedException();

    public virtual IResult UnstagePath(string path) => throw new NotImplementedException();

    public virtual IResult StashChanges() => throw new NotImplementedException();

    public virtual IResult PopChanges() => throw new NotImplementedException();

    public virtual IResult RestorePath(string path) => throw new NotImplementedException();

    public virtual Task<ValueResult<string>> CommitChangesAsync(string commitMessage) =>
        throw new NotImplementedException();

    public virtual Task<ValueResult<string>> PushChangesAsync() =>
        throw new NotImplementedException();
}
