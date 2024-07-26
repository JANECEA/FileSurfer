using System;
using System.IO;

namespace FileSurfer;

public class MoveDirToTrash : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly string _dirPath;

    public MoveDirToTrash(IFileOperationsHandler fileHandler, string dirPath)
    {
        _fileOpsHandler= fileHandler;
        _dirPath = dirPath;
    }

    public bool Redo(out string? errorMessage) =>
        _fileOpsHandler.MoveDirToTrash(_dirPath, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileOpsHandler.RestoreDir(_dirPath, out errorMessage);
}

public class MoveFileToTrash : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly string _filePath;

    public MoveFileToTrash(IFileOperationsHandler fileHandler, string filePath)
    {
        _fileOpsHandler= fileHandler;
        _filePath = filePath;
    }

    public bool Redo(out string? errorMessage) =>
        _fileOpsHandler.MoveFileToTrash(_filePath, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileOpsHandler.RestoreFile(_filePath, out errorMessage);
}

public class MoveFileTo : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly string _fileName;
    private readonly string _oldDir;
    private readonly string _newDir;

    public MoveFileTo(IFileOperationsHandler fileHandler, string oldPath, string destinationDir)
    {
        _fileOpsHandler = fileHandler;
        _fileName = Path.GetFileName(oldPath);
        _oldDir = Path.GetDirectoryName(oldPath);
        _newDir = destinationDir;

        _oldDir = _oldDir ?? throw new ArgumentNullException(_oldDir);
    }

    public bool Redo(out string? errorMessage) => 
        _fileOpsHandler.MoveFileTo(Path.Combine(_oldDir, _fileName), Path.Combine(_newDir, _fileName), out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileOpsHandler.MoveFileTo(Path.Combine(_newDir, _fileName), Path.Combine(_oldDir, _fileName), out errorMessage);
}

public class MoveDirTo : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly string _dirName;
    private readonly string _oldDir;
    private readonly string _newDir;

    public MoveDirTo(IFileOperationsHandler fileHandler, string oldPath, string destinationDir)
    {
        _fileOpsHandler = fileHandler;
        _dirName = Path.GetFileName(oldPath);
        _oldDir = Path.GetDirectoryName(oldPath);
        _newDir = destinationDir;

        _oldDir = _oldDir ?? throw new ArgumentNullException(_oldDir);
    }

    public bool Redo(out string? errorMessage) =>
        _fileOpsHandler.MoveDirTo(Path.Combine(_oldDir, _dirName), Path.Combine(_newDir, _dirName), out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileOpsHandler.MoveDirTo(Path.Combine(_newDir, _dirName), Path.Combine(_oldDir, _dirName), out errorMessage);
}

public class CopyFileTo : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly string _oldPath;
    private readonly string _newPath;

    public CopyFileTo(IFileOperationsHandler fileHandler, string path, string destinationDir)
    {
        _fileOpsHandler= fileHandler;
        _oldPath = path;
        _newPath = Path.Combine(destinationDir, Path.GetFileName(path));
    }

    public bool Redo(out string? errorMessage) =>
        _fileOpsHandler.CopyFileTo(_oldPath, _newPath, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileOpsHandler.DeleteFile(_newPath, out errorMessage);
}

public class CopyDirTo : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly string _oldPath;
    private readonly string _newPath;

    public CopyDirTo(IFileOperationsHandler fileHandler, string path, string destinationDir)
    {
        _fileOpsHandler= fileHandler;
        _oldPath = path;
        _newPath = Path.Combine(destinationDir, Path.GetFileName(path));
    }

    public bool Redo(out string? errorMessage) =>
        _fileOpsHandler.CopyDirTo(_oldPath, _newPath, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileOpsHandler.DeleteDir(_newPath, out errorMessage);
}

public class RenameFile : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly string _dir;
    private readonly string _oldName;
    private readonly string _newName;

    public RenameFile(IFileOperationsHandler fileHandler, string path, string newName)
    {
        _fileOpsHandler= fileHandler;
        _dir = Path.GetDirectoryName(path);
        _oldName = Path.GetFileName(path);
        _newName = newName;
        
        _dir = _dir ?? throw new ArgumentNullException(_dir);
    }

    public bool Redo(out string? errorMessage) =>
        _fileOpsHandler.RenameFileAt(Path.Combine(_dir, _oldName), _newName, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileOpsHandler.RenameFileAt(Path.Combine(_dir, _newName), _oldName, out errorMessage);
}

public class RenameDir : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly string _dir;
    private readonly string _oldName;
    private readonly string _newName;

    public RenameDir(IFileOperationsHandler fileHandler, string path, string newName)
    {
        _fileOpsHandler= fileHandler;
        _dir = Path.GetDirectoryName(path);
        _oldName = Path.GetFileName(path);
        _newName = newName;

        _dir = _dir ?? throw new ArgumentNullException(_dir);
    }

    public bool Redo(out string? errorMessage) =>
        _fileOpsHandler.RenameDirAt(Path.Combine(_dir, _oldName), _newName, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileOpsHandler.RenameDirAt(Path.Combine(_dir, _newName), _oldName, out errorMessage);
}

public class NewFileAt : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly string _path;
    private readonly string _fileName;

    public NewFileAt(IFileOperationsHandler fileHandler, string path, string fileName)
    {
        _fileOpsHandler= fileHandler;
        _path = path;
        _fileName = fileName;
    }

    public bool Redo(out string? errorMessage) =>
        _fileOpsHandler.NewFileAt(_path, _fileName, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileOpsHandler.DeleteFile(Path.Combine(_path, _fileName), out errorMessage);
}

public class NewDirAt : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly string _path;
    private readonly string _dirName;

    public NewDirAt(IFileOperationsHandler fileHandler, string path, string dirName)
    {
        _fileOpsHandler= fileHandler;
        _path = path;
        _dirName = dirName;
    }

    public bool Redo(out string? errorMessage) =>
        _fileOpsHandler.NewDirAt(_path, _dirName, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileOpsHandler.DeleteDir(Path.Combine(_path, _dirName), out errorMessage);
}

