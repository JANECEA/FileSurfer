using System;
using System.Collections.Generic;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileOperations;
using FileSurfer.Core.Models.Shell;
using FileSurfer.Core.Models.VersionControl;

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

public class SftpGitVersionControl : IVersionControl
{
    public bool InitIfVersionControlled(string directoryPath) => false;

    public VcStatus GetStatus(string filePath) => VcStatus.NotVersionControlled;

    public IResult DownloadChanges() => SimpleResult.Error("Unsupported environment.");

    public string[] GetBranches() => Array.Empty<string>();

    public string GetCurrentBranchName() => string.Empty;

    public IResult SwitchBranches(string branchName) =>
        SimpleResult.Error("Unsupported environment.");

    public IResult StagePath(string path) => SimpleResult.Error("Unsupported environment.");

    public IResult UnstagePath(string filePath) => SimpleResult.Error("Unsupported environment.");

    public IResult CommitChanges(string commitMessage) =>
        SimpleResult.Error("Unsupported environment.");

    public IResult UploadChanges() => SimpleResult.Error("Unsupported environment.");

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
