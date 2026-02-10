using System;
using System.Collections.Generic;
using FileSurfer.Core.Models.FileOperations;
using FileSurfer.Core.Models.Shell;
using FileSurfer.Core.Models.VersionControl;
using FileSurfer.Core.ViewModels;

// Models that might be implemented for Sftp in the future

namespace FileSurfer.Core.Models.Sftp;

public class SftpArchiveManager : IArchiveManager
{
    public bool IsZipped(string filePath) => false;

    public IResult ZipFiles(
        IEnumerable<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName
    ) => SimpleResult.Error("Unsupported environment.");

    public IResult UnzipArchive(string archivePath, string destinationPath) =>
        SimpleResult.Error("Unsupported environment.");
}

public class SftpGitIntegration : IGitIntegration
{
    public bool InitIfGitRepository(string directoryPath) => false;

    public GitStatus GetStatus(string filePath) => GitStatus.NotVersionControlled;

    public IResult PullChanges() => SimpleResult.Error("Unsupported environment.");

    public string[] GetBranches() => Array.Empty<string>();

    public string GetCurrentBranchName() => string.Empty;

    public IResult SwitchBranches(string branchName) =>
        SimpleResult.Error("Unsupported environment.");

    public IResult StagePath(string path) => SimpleResult.Error("Unsupported environment.");

    public IResult UnstagePath(string filePath) => SimpleResult.Error("Unsupported environment.");

    public IResult CommitChanges(string commitMessage) =>
        SimpleResult.Error("Unsupported environment.");

    public IResult PushChanges() => SimpleResult.Error("Unsupported environment.");

    public void Dispose() { }
}

public class SftpBinInteraction : IBinInteraction
{
    public IResult MoveFileToTrash(string filePath) =>
        SimpleResult.Error("Unsupported environment.");

    public IResult MoveDirToTrash(string dirPath) => SimpleResult.Error("Unsupported environment.");

    public IResult RestoreFile(string originalFilePath) =>
        SimpleResult.Error("Unsupported environment.");

    public IResult RestoreDir(string originalDirPath) =>
        SimpleResult.Error("Unsupported environment.");
}

public class SftpFileProperties : IFileProperties
{
    public IResult ShowFileProperties(FileSystemEntryViewModel entry) =>
        SimpleResult.Error("Unsupported environment.");

    public bool SupportsOpenAs(IFileSystemEntry entry) => false;

    public IResult ShowOpenAsDialog(IFileSystemEntry entry) =>
        SimpleResult.Error("Unsupported environment.");
}

public class SftpStubShellHandler : IShellHandler
{
    public IResult CreateFileLink(string filePath) =>
        SimpleResult.Error("Server refused ssh connection");

    public IResult CreateDirectoryLink(string dirPath) =>
        SimpleResult.Error("Server refused ssh connection");

    public IResult OpenCmdAt(string dirPath) => SimpleResult.Error("Server refused ssh connection");

    public ValueResult<string> ExecuteCommand(string programName, params string[] args) =>
        ValueResult<string>.Error("Server refused ssh connection");
}
