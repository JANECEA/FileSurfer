using System;
using System.IO;

namespace FileSurfer.UndoableFileOperations;

public class MoveFilesToTrash : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly FileSystemEntry[] _entries;

    public MoveFilesToTrash(IFileOperationsHandler fileHandler, FileSystemEntry[] entries)
    {
        _fileOpsHandler = fileHandler;
        _entries = entries;
    }

    public bool Redo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occurred moving these files to trash:";
        foreach (FileSystemEntry entry in _entries)
        {
            bool result = entry.IsDirectory
                ? _fileOpsHandler.MoveDirToTrash(entry.PathToEntry, out _)
                : _fileOpsHandler.MoveFileToTrash(entry.PathToEntry, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{entry.PathToEntry}\",";
        }
        errorMessage?.TrimEnd(',');
        if (!errorOccured)
            errorMessage = null;
        return !errorOccured;
    }

    public bool Undo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occurred restoring these files:";
        foreach (FileSystemEntry entry in _entries)
        {
            bool result = entry.IsDirectory
                ? _fileOpsHandler.RestoreDir(entry.PathToEntry, out _)
                : _fileOpsHandler.RestoreFile(entry.PathToEntry, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{entry.PathToEntry}\",";
        }
        errorMessage?.TrimEnd(',');
        if (!errorOccured)
            errorMessage = null;
        return !errorOccured;
    }
}

public class MoveFilesTo : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly FileSystemEntry[] _entries;
    private readonly string _destinationDir;
    private readonly string _originalDir;

    public MoveFilesTo(
        IFileOperationsHandler fileOpsHandler,
        FileSystemEntry[] entries,
        string destinationDir
    )
    {
        _entries = entries;
        _fileOpsHandler = fileOpsHandler;
        _destinationDir = destinationDir;
        _originalDir =
            Path.GetDirectoryName(entries[0].PathToEntry) ?? throw new InvalidOperationException();
    }

    public bool Redo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured moving these files:";
        foreach (FileSystemEntry entry in _entries)
        {
            bool result = entry.IsDirectory
                ? _fileOpsHandler.MoveDirTo(entry.PathToEntry, _destinationDir, out _)
                : _fileOpsHandler.MoveFileTo(entry.PathToEntry, _destinationDir, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{entry.PathToEntry}\",";
        }
        errorMessage?.TrimEnd(',');
        if (!errorOccured)
            errorMessage = null;
        return !errorOccured;
    }

    public bool Undo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured moving these files:";
        foreach (FileSystemEntry entry in _entries)
        {
            string newPath = Path.Combine(_destinationDir, entry.Name);
            bool result = entry.IsDirectory
                ? _fileOpsHandler.MoveDirTo(newPath, _originalDir, out _)
                : _fileOpsHandler.MoveFileTo(newPath, _originalDir, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{entry.PathToEntry}\",";
        }
        errorMessage?.TrimEnd(',');
        if (!errorOccured)
            errorMessage = null;
        return !errorOccured;
    }
}

public class CopyFilesTo : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly FileSystemEntry[] _entries;
    private readonly string _destinationDir;

    public CopyFilesTo(
        IFileOperationsHandler fileHandler,
        FileSystemEntry[] entries,
        string destinationDir
    )
    {
        _fileOpsHandler = fileHandler;
        _entries = entries;
        _destinationDir = destinationDir;
    }

    public bool Redo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured copying these files:";
        foreach (FileSystemEntry entry in _entries)
        {
            bool result = entry.IsDirectory
                ? _fileOpsHandler.CopyDirTo(entry.PathToEntry, _destinationDir, out _)
                : _fileOpsHandler.CopyFileTo(entry.PathToEntry, _destinationDir, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{entry.PathToEntry}\",";
        }
        errorMessage?.TrimEnd(',');
        if (!errorOccured)
            errorMessage = null;
        return !errorOccured;
    }

    public bool Undo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured deleting these files:";
        foreach (FileSystemEntry entry in _entries)
        {
            string newPath = Path.Combine(_destinationDir, entry.Name);
            bool result = entry.IsDirectory
                ? _fileOpsHandler.DeleteDir(newPath, out _)
                : _fileOpsHandler.DeleteFile(newPath, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{entry.PathToEntry}\",";
        }
        errorMessage?.TrimEnd(',');
        if (!errorOccured)
            errorMessage = null;
        return !errorOccured;
    }
}

public class DuplicateFiles : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly FileSystemEntry[] _entries;
    private readonly string[] _copyNames;
    private readonly string _parentDir;

    public DuplicateFiles(
        IFileOperationsHandler fileOpsHandler,
        FileSystemEntry[] entries,
        string[] copyNames
    )
    {
        _fileOpsHandler = fileOpsHandler;
        _entries = entries;
        _copyNames = copyNames;
        _parentDir =
            Path.GetDirectoryName(entries[0].PathToEntry) ?? throw new InvalidOperationException();
    }

    public bool Redo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured duplicating these files:";
        for (int i = 0; i < _entries.Length; i++)
        {
            bool result = _entries[i].IsDirectory
                ? _fileOpsHandler.DuplicateDir(_entries[i].PathToEntry, _copyNames[i], out _)
                : _fileOpsHandler.DuplicateFile(_entries[i].PathToEntry, _copyNames[i], out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{_entries[i].PathToEntry}\",";
        }
        errorMessage?.TrimEnd(',');
        if (!errorOccured)
            errorMessage = null;
        return !errorOccured;
    }

    public bool Undo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured deleting these files:";
        for (int i = 0; i < _entries.Length; i++)
        {
            bool result = _entries[i].IsDirectory
                ? _fileOpsHandler.DeleteDir(Path.Combine(_parentDir, _copyNames[i]), out _)
                : _fileOpsHandler.DeleteFile(Path.Combine(_parentDir, _copyNames[i]), out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{_entries[i].PathToEntry}\",";
        }
        errorMessage?.TrimEnd(',');
        if (!errorOccured)
            errorMessage = null;
        return !errorOccured;
    }
}

public class RenameMultiple : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly FileSystemEntry[] _entries;
    private readonly string[] _newNames;
    private readonly string _dirName;
    private readonly string? _extension = null;

    public RenameMultiple(
        IFileOperationsHandler fileHandler,
        FileSystemEntry[] entries,
        string[] newNames
    )
    {
        _fileOpsHandler = fileHandler;
        _entries = entries;
        _newNames = newNames;
        _dirName =
            Path.GetDirectoryName(entries[0].PathToEntry) ?? throw new InvalidOperationException();

        if (!entries[0].IsDirectory)
        {
            _extension = Path.GetExtension(entries[0].PathToEntry);
            _extension = _extension == string.Empty ? null : _extension;
        }
    }

    public bool Redo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured renaming these files:";
        for (int i = 0; i < _entries.Length; i++)
        {
            bool result = _entries[i].IsDirectory
                ? _fileOpsHandler.RenameDirAt(_entries[i].PathToEntry, _newNames[i], out _)
                : _fileOpsHandler.RenameFileAt(_entries[i].PathToEntry, _newNames[i], out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{_entries[i].PathToEntry}\",";
        }
        errorMessage?.TrimEnd(',');
        if (!errorOccured)
            errorMessage = null;
        return !errorOccured;
    }

    public bool Undo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured renaming these files:";
        for (int i = 0; i < _entries.Length; i++)
        {
            string newPath = Path.Combine(_dirName, _newNames[i]);
            bool result = _entries[i].IsDirectory
                ? _fileOpsHandler.RenameDirAt(newPath, _entries[i].Name, out _)
                : _fileOpsHandler.RenameFileAt(newPath, _entries[i].Name, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{_entries[i].PathToEntry}\",";
        }
        errorMessage?.TrimEnd(',');
        if (!errorOccured)
            errorMessage = null;
        return !errorOccured;
    }
}

public class RenameOne : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly FileSystemEntry _entry;
    private readonly string _newName;
    private readonly string _newPath;

    public RenameOne(IFileOperationsHandler fileHandler, FileSystemEntry entry, string newName)
    {
        _fileOpsHandler = fileHandler;
        _entry = entry;
        _newName = newName;
        string dirName =
            Path.GetDirectoryName(entry.PathToEntry) ?? throw new InvalidOperationException();
        _newPath = Path.Combine(dirName, newName);
    }

    public bool Redo(out string? errorMessage) =>
        _entry.IsDirectory
            ? _fileOpsHandler.RenameDirAt(_entry.PathToEntry, _newName, out errorMessage)
            : _fileOpsHandler.RenameFileAt(_entry.PathToEntry, _newName, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _entry.IsDirectory
            ? _fileOpsHandler.RenameDirAt(_newPath, _entry.Name, out errorMessage)
            : _fileOpsHandler.RenameFileAt(_newPath, _entry.Name, out errorMessage);
}

public class NewFileAt : IUndoableFileOperation
{
    private readonly IFileOperationsHandler _fileOpsHandler;
    private readonly string _path;
    private readonly string _fileName;

    public NewFileAt(IFileOperationsHandler fileHandler, string path, string fileName)
    {
        _fileOpsHandler = fileHandler;
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
        _fileOpsHandler = fileHandler;
        _path = path;
        _dirName = dirName;
    }

    public bool Redo(out string? errorMessage) =>
        _fileOpsHandler.NewDirAt(_path, _dirName, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileOpsHandler.DeleteDir(Path.Combine(_path, _dirName), out errorMessage);
}
