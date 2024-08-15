using System;
using System.Drawing;

namespace FileSurfer;

public interface IFileOperationsHandler
{
    public string[] GetDrives(out string? errorMessage);

    public bool OpenFile(string filePath, out string? errorMessage);

    public bool ShowProperties(string filePath, out string? errorMessage);

    public bool CreateLink(string filePath, out string? errorMessage);

    public string[] GetPathDirs(string path, bool includeHidden, bool includeProtectedByOS);

    public string[] GetPathFiles(string path, bool includeHidden, bool includeProtectedByOS);

    public long GetFileSizeB(string path);

    public DateTime? GetFileLastModified(string filePath);

    public DateTime? GetDirLastModified(string dirPath);

    public Icon? GetFileIcon(string path);

    public bool OpenCmdAt(string dirPath, out string? errorMessage);

    public bool ExecuteCmd(string command, out string? errorMessage);

    public bool IsHidden(string path, bool isDirectory);

    public bool NewFileAt(string dirPath, string fileName, out string? errorMessage);

    public bool NewDirAt(string dirPath, string DirName, out string? errorMessage);

    public bool CopyFileToSystemClipBoard(string filePath, out string? errorMessage);

    public bool PasteFileFromClipBoardAt(string filePath, out string? errorMessage);

    public bool IsValidFileName(string fileName);

    public bool IsValidDirName(string dirName);

    public bool RenameFileAt(string filePath, string newName, out string? errorMessage);

    public bool RenameDirAt(string dirPath, string newName, out string? errorMessage);

    public bool MoveFileTo(string filePath, string destinationDir, out string? errorMessage);

    public bool MoveDirTo(string dirPath, string destinationDir, out string? errorMessage);

    public bool CopyFileTo(string filePath, string nameOfCopy, string destinationDir, out string? errorMessage);

    public bool CopyDirTo(string dirPath, string nameOfCopy, string destinationDir, out string? errorMessage);

    public bool MoveFileToTrash(string filePath, out string? errorMessage);

    public bool MoveDirToTrash(string dirPath, out string? errorMessage);

    public bool RestoreFile(string ogFilePath, out string? errorMessage);

    public bool RestoreDir(string ogDirPath, out string? errorMessage);

    public bool DeleteFile(string filePath, out string? errorMessage);

    public bool DeleteDir(string dirPath, out string? errorMessage);
}
