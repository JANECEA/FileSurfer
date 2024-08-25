using System;
using System.Drawing;
using System.IO;

namespace FileSurfer.Models;

public interface IFileOperationsHandler
{
    public DriveInfo[] GetDrives();

    public string[] GetSpecialFolders();

    public bool OpenFile(string filePath, out string? errorMessage);

    public bool CreateLink(string filePath, out string? errorMessage);

    public string[] GetPathDirs(string path, bool includeHidden, bool includeOS);

    public string[] GetPathFiles(string path, bool includeHidden, bool includeOS);

    public long GetFileSizeB(string path);

    public DateTime? GetFileLastModified(string filePath);

    public DateTime? GetDirLastModified(string dirPath);

    public Bitmap? GetFileIcon(string path);

    public bool OpenCmdAt(string dirPath, out string? errorMessage);

    public bool ExecuteCmd(string command);

    public bool IsHidden(string path, bool isDirectory);

    public bool NewFileAt(string dirPath, string fileName, out string? errorMessage);

    public bool NewDirAt(string dirPath, string DirName, out string? errorMessage);

    public bool RenameFileAt(string filePath, string newName, out string? errorMessage);

    public bool RenameDirAt(string dirPath, string newName, out string? errorMessage);

    public bool MoveFileTo(string filePath, string destinationDir, out string? errorMessage);

    public bool MoveDirTo(string dirPath, string destinationDir, out string? errorMessage);

    public bool CopyFileTo(string filePath, string destinationDir, out string? errorMessage);

    public bool CopyDirTo(string dirPath, string destinationDir, out string? errorMessage);

    public bool DuplicateFile(string filePath, string copyName, out string? errorMessage);

    public bool DuplicateDir(string dirPath, string copyName, out string? errorMessage);

    public bool MoveFileToTrash(string filePath, out string? errorMessage);

    public bool MoveDirToTrash(string dirPath, out string? errorMessage);

    public bool RestoreFile(string ogFilePath, out string? errorMessage);

    public bool RestoreDir(string ogDirPath, out string? errorMessage);

    public bool DeleteFile(string filePath, out string? errorMessage);

    public bool DeleteDir(string dirPath, out string? errorMessage);
}
