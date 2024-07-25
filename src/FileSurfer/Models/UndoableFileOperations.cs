using System;
using System.IO;

namespace FileSurfer;

public class MoveDirToTrash : IUndoableFileOperation
{
    readonly IFileOperationsHandler _fileOpsHandler;


    public bool Redo(out string? errorMessage)
    {
        throw new System.NotImplementedException();
    }

    public bool Undo(out string? errorMessage)
    {
        throw new System.NotImplementedException();
    }
}

public class MoveFileToTrash : IUndoableFileOperation
{
    readonly IFileOperationsHandler _fileOpsHandler;

    public bool Redo(out string? errorMessage)
    {
        throw new System.NotImplementedException();
    }

    public bool Undo(out string? errorMessage)
    {
        throw new System.NotImplementedException();
    }
}

public class MoveFileTo : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly string _fileName;
    private readonly string _oldDir;
    private readonly string _newDir;

    public MoveFileTo(string oldPath, string destinationDir)
    {
        _fileName = Path.GetFileName(oldPath);
        _oldDir = Path.GetDirectoryName(oldPath);
        _newDir = destinationDir;

        _oldDir = _oldDir
            ?? throw new ArgumentNullException(_oldDir);
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

        _oldDir = _oldDir
            ?? throw new ArgumentNullException(_oldDir);
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
        _fileOpsHandler.DeleteDirectory(_newPath, out errorMessage);
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
    }

    public bool Redo(out string? errorMessage) =>
        _fileOpsHandler.RenameDirAt(Path.Combine(_dir, _oldName), _newName, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileOpsHandler.RenameDirAt(Path.Combine(_dir, _newName), _oldName, out errorMessage);
}

public class NewFileAt : IUndoableFileOperation
{
    public bool Redo(out string? errorMessage)
    {
        throw new System.NotImplementedException();
    }

    public bool Undo(out string? errorMessage)
    {
        throw new System.NotImplementedException();
    }
}

public class NewDirAt : IUndoableFileOperation
{
    public bool Redo(out string? errorMessage)
    {
        throw new System.NotImplementedException();
    }

    public bool Undo(out string? errorMessage)
    {
        throw new System.NotImplementedException();
    }
}

