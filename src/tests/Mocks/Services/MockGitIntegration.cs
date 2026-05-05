using FileSurfer.Core.Models;
using FileSurfer.Core.Services.VersionControl;

namespace Mocks.Services;

public class MockGitIntegration : ServiceMock, IGitIntegration
{
    public virtual void Dispose() => RecordCall(nameof(Dispose));

    public virtual bool InitIfGitRepository(string directoryPath)
    {
        RecordCall(nameof(InitIfGitRepository), directoryPath);
        return true;
    }

    public virtual GitStatus GetStatus(string filePath)
    {
        RecordCall(nameof(GetStatus), filePath);
        return GitStatus.NotVersionControlled;
    }

    public virtual Task<IResult> FetchChangesAsync()
    {
        RecordCall(nameof(FetchChangesAsync));
        return Task.FromResult<IResult>(SimpleResult.Ok());
    }

    public virtual Task<ValueResult<string>> PullChangesAsync()
    {
        RecordCall(nameof(PullChangesAsync));
        return Task.FromResult(ValueResult<string>.Ok("Mock pull completed."));
    }

    public virtual RepoDetails? GetRepositoryState()
    {
        RecordCall(nameof(GetRepositoryState));
        return new RepoDetails(0, 0);
    }

    public virtual string[] GetBranches()
    {
        RecordCall(nameof(GetBranches));
        return ["main"];
    }

    public virtual string GetCurrentBranchName()
    {
        RecordCall(nameof(GetCurrentBranchName));
        return "main";
    }

    public virtual IResult SwitchBranches(string branchName)
    {
        RecordCall(nameof(SwitchBranches), branchName);
        return SimpleResult.Ok();
    }

    public virtual IResult StagePath(string path)
    {
        RecordCall(nameof(StagePath), path);
        return SimpleResult.Ok();
    }

    public virtual IResult UnstagePath(string path)
    {
        RecordCall(nameof(UnstagePath), path);
        return SimpleResult.Ok();
    }

    public virtual IResult StashChanges()
    {
        RecordCall(nameof(StashChanges));
        return SimpleResult.Ok();
    }

    public virtual IResult PopChanges()
    {
        RecordCall(nameof(PopChanges));
        return SimpleResult.Ok();
    }

    public virtual IResult RestorePath(string path)
    {
        RecordCall(nameof(RestorePath), path);
        return SimpleResult.Ok();
    }

    public virtual Task<ValueResult<string>> CommitChangesAsync(string commitMessage)
    {
        RecordCall(nameof(CommitChangesAsync), commitMessage);
        return Task.FromResult(ValueResult<string>.Ok("Mock commit completed."));
    }

    public virtual Task<ValueResult<string>> PushChangesAsync()
    {
        RecordCall(nameof(PushChangesAsync));
        return Task.FromResult(ValueResult<string>.Ok("Mock push completed."));
    }
}
