namespace FileSurfer;

public interface IFileOperationsHandler
{
    public string[] GetDrives(out string? errorMessage);

    public bool OpenFile(string filePath, out string? errorMessage);

    public string[] GetDirFiles(string dirPath, out string? errorMessage);

    public string[] GetDirFolders(string dirPath, out string? errorMessage);

    public long? GetFileSizeKiB(string path, out string? errorMessage);

    public object GetFileIcon(string path, out string? errorMessage);

    public object GetFileContextMenu(string path, out string? errorMessage);

    public bool OpenCmdAt(string dirPath, out string? errorMessage);

    public bool NewFileAt(string dirPath, string fileName, out string? errorMessage);

    public bool NewDirAt(string dirPath, string DirName, out string? errorMessage);

    public bool CopyFileToSystemClipBoard(string filePath, out string? errorMessage);

    public bool PasteFileFromClipBoardAt(string filePath, out string? errorMessage);

    public bool RenameFileAt(string filePath, string newName, out string? errorMessage);

    public bool RenameDirAt(string filePath, string newName, out string? errorMessage);

    public bool MoveFileTo(string filePath, string destinationDir, out string? errorMessage);

    public bool MoveDirTo(string dirPath, string destinationDir, out string? errorMessage);

    public bool MoveFileToTrash(string filePath, out string? errorMessage);

    public bool MoveDirToTrash(string dirPath, out string? errorMessage);

    public bool DeleteFile(string filePath, out string? errorMessage);

    public bool DeleteDirectory(string dirPath, out string? errorMessage);
}
