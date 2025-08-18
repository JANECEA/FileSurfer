using System;
using System.IO;
using FileSurfer.Models.FileInformation;

namespace FileSurfer.Models.FileOperations.Undoable;

/// <summary>
/// Represents the action of moving selected files to the system trash.
/// </summary>
public class MoveFilesToTrash : UndoableOperation
{
    protected override string RedoErrorStart => "Problems occurred moving these files to trash:";
    protected override string UndoErrorStart => "Problems occurred restoring these files:";

    public MoveFilesToTrash(IFileIOHandler fileIOHandler, IFileSystemEntry[] entries)
        : base(fileIOHandler, entries) { }

    protected override bool RedoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.MoveDirToTrash(entry.PathToEntry, out _)
            : _fileIOHandler.MoveFileToTrash(entry.PathToEntry, out _);

    protected override bool UndoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.RestoreDir(entry.PathToEntry, out _)
            : _fileIOHandler.RestoreFile(entry.PathToEntry, out _);
}

/// <summary>
/// Represents the action of moving files and directories to a specific directory.
/// </summary>
public class MoveFilesTo : UndoableOperation
{
    private readonly string _destinationDir;
    private readonly string _originalDir;
    protected override string RedoErrorStart => "Problems occured moving these files:";
    protected override string UndoErrorStart => "Problems occured moving these files:";

    public MoveFilesTo(
        IFileIOHandler fileIOHandler,
        IFileSystemEntry[] entries,
        string destinationDir
    )
        : base(fileIOHandler, entries)
    {
        _destinationDir = destinationDir;
        _originalDir =
            Path.GetDirectoryName(entries[0].PathToEntry) ?? throw new InvalidOperationException();
    }

    protected override bool RedoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.MoveDirTo(entry.PathToEntry, _destinationDir, out _)
            : _fileIOHandler.MoveFileTo(entry.PathToEntry, _destinationDir, out _);

    protected override bool UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = Path.Combine(_destinationDir, entry.Name);
        return entry is DirectoryEntry
            ? _fileIOHandler.MoveDirTo(newPath, _originalDir, out _)
            : _fileIOHandler.MoveFileTo(newPath, _originalDir, out _);
    }
}

/// <summary>
/// Represents the action of copying files and directories to a specific directory.
/// </summary>
public class CopyFilesTo : UndoableOperation
{
    private readonly string _destinationDir;
    protected override string RedoErrorStart => "Problems occured copying these files:";
    protected override string UndoErrorStart => "Problems occured deleting these files:";

    public CopyFilesTo(
        IFileIOHandler fileIOHandler,
        IFileSystemEntry[] entries,
        string destinationDir
    )
        : base(fileIOHandler, entries) => _destinationDir = destinationDir;

    protected override bool RedoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.CopyDirTo(entry.PathToEntry, _destinationDir, out _)
            : _fileIOHandler.CopyFileTo(entry.PathToEntry, _destinationDir, out _);

    protected override bool UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = Path.Combine(_destinationDir, entry.Name);
        return entry is DirectoryEntry
            ? _fileIOHandler.DeleteDir(newPath, out _)
            : _fileIOHandler.DeleteFile(newPath, out _);
    }
}

/// <summary>
/// Represents the action of duplicating files or directories in a specific directory.
/// </summary>
public class DuplicateFiles : UndoableOperation
{
    private readonly string[] _copyNames;
    private readonly string _parentDir;
    protected override string RedoErrorStart => "Problems occured duplicating these files:";
    protected override string UndoErrorStart => "Problems occured deleting these files:";

    public DuplicateFiles(
        IFileIOHandler fileIOHandler,
        IFileSystemEntry[] entries,
        string[] copyNames
    )
        : base(fileIOHandler, entries)
    {
        _copyNames = copyNames;
        _parentDir =
            Path.GetDirectoryName(entries[0].PathToEntry) ?? throw new InvalidOperationException();
    }

    protected override bool RedoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.DuplicateDir(entry.PathToEntry, _copyNames[index], out _)
            : _fileIOHandler.DuplicateFile(entry.PathToEntry, _copyNames[index], out _);

    protected override bool UndoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.DeleteDir(Path.Combine(_parentDir, _copyNames[index]), out _)
            : _fileIOHandler.DeleteFile(Path.Combine(_parentDir, _copyNames[index]), out _);
}

/// <summary>
/// Represents the action of renaming multiple files or directories.
/// </summary>
public class RenameMultiple : UndoableOperation
{
    private readonly string[] _newNames;
    private readonly string _dirName;
    protected override string RedoErrorStart => "Problems occured renaming these files:";
    protected override string UndoErrorStart => RedoErrorStart;

    public RenameMultiple(
        IFileIOHandler fileIOHandler,
        IFileSystemEntry[] entries,
        string[] newNames
    )
        : base(fileIOHandler, entries)
    {
        _newNames = newNames;
        _dirName =
            Path.GetDirectoryName(entries[0].PathToEntry) ?? throw new InvalidOperationException();
    }

    protected override bool RedoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.RenameDirAt(entry.PathToEntry, _newNames[index], out _)
            : _fileIOHandler.RenameFileAt(entry.PathToEntry, _newNames[index], out _);

    protected override bool UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = Path.Combine(_dirName, _newNames[index]);
        return entry is DirectoryEntry
            ? _fileIOHandler.RenameDirAt(newPath, _entries[index].Name, out _)
            : _fileIOHandler.RenameFileAt(newPath, _entries[index].Name, out _);
    }
}

/// <summary>
/// Represents the action of flattening a directory.
/// </summary>
public class FlattenFolder : IUndoableFileOperation
{
    private readonly IFileIOHandler _fileIOHandler;
    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly string _dirPath;
    private readonly string? _parentDir;
    private readonly bool _showHidden;
    private readonly bool _showProtected;

    private string[] _containedDirs = Array.Empty<string>();
    private string[] _containedFiles = Array.Empty<string>();

    public FlattenFolder(
        IFileIOHandler fileIOHandler,
        IFileInfoProvider fileInfoProvider,
        string dirPath
    )
    {
        _fileIOHandler = fileIOHandler;
        _fileInfoProvider = fileInfoProvider;
        _dirPath = dirPath;
        _parentDir = Path.GetDirectoryName(dirPath);
        _showHidden = FileSurferSettings.ShowHiddenFiles;
        _showProtected = FileSurferSettings.ShowProtectedFiles;
    }

    public bool Redo(out string? errorMessage)
    {
        errorMessage = $"Cannot flatten top level directory: \"{_dirPath}\"";
        if (_parentDir is null)
            return false;

        _containedDirs = _fileInfoProvider.GetPathDirs(_dirPath, _showHidden, _showProtected);
        _containedFiles = _fileInfoProvider.GetPathFiles(_dirPath, _showHidden, _showProtected);

        bool success = true;
        errorMessage = "Problems occured moving these files:";
        foreach (string containedDir in _containedDirs)
        {
            bool result = _fileIOHandler.MoveDirTo(containedDir, _parentDir, out _);

            success &= result;
            if (!result)
                errorMessage += $" \"{Path.GetFileName(containedDir)}\",";
        }
        foreach (string containedFile in _containedFiles)
        {
            bool result = _fileIOHandler.MoveFileTo(containedFile, _parentDir, out _);

            success &= result;
            if (!result)
                errorMessage += $" \"{Path.GetFileName(containedFile)}\",";
        }

        errorMessage = success ? null : errorMessage.TrimEnd(',');
        return success && _fileIOHandler.DeleteDir(_dirPath, out errorMessage);
    }

    public bool Undo(out string? errorMessage)
    {
        errorMessage = $"Cannot create a top level directory: \"{_dirPath}\"";
        if (
            _parentDir is null
            || !_fileIOHandler.NewDirAt(_parentDir, Path.GetFileName(_dirPath), out errorMessage)
        )
            return false;

        bool success = true;
        errorMessage = "Problems occured moving these files:";
        foreach (string containedDir in _containedDirs)
        {
            bool result = _fileIOHandler.MoveDirTo(
                Path.Combine(_parentDir, Path.GetFileName(containedDir)),
                _dirPath,
                out _
            );

            success &= result;
            if (!result)
                errorMessage += $" \"{Path.GetFileName(containedDir)}\",";
        }
        foreach (string containedFile in _containedFiles)
        {
            bool result = _fileIOHandler.MoveFileTo(
                Path.Combine(_parentDir, Path.GetFileName(containedFile)),
                _dirPath,
                out _
            );

            success &= result;
            if (!result)
                errorMessage += $" \"{Path.GetFileName(containedFile)}\",";
        }

        errorMessage = success ? null : errorMessage.TrimEnd(',');
        return success;
    }
}

/// <summary>
/// Represents the action of renaming one file or directory.
/// </summary>
public class RenameOne : IUndoableFileOperation
{
    private readonly IFileIOHandler _fileIOHandler;
    private readonly IFileSystemEntry _entry;
    private readonly string _newName;
    private readonly string _newPath;

    public RenameOne(IFileIOHandler fileHandler, IFileSystemEntry entry, string newName)
    {
        _fileIOHandler = fileHandler;
        _entry = entry;
        _newName = newName;
        string dirName =
            Path.GetDirectoryName(entry.PathToEntry) ?? throw new InvalidOperationException();
        _newPath = Path.Combine(dirName, newName);
    }

    public bool Redo(out string? errorMessage) =>
        _entry is DirectoryEntry
            ? _fileIOHandler.RenameDirAt(_entry.PathToEntry, _newName, out errorMessage)
            : _fileIOHandler.RenameFileAt(_entry.PathToEntry, _newName, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _entry is DirectoryEntry
            ? _fileIOHandler.RenameDirAt(_newPath, _entry.Name, out errorMessage)
            : _fileIOHandler.RenameFileAt(_newPath, _entry.Name, out errorMessage);
}

/// <summary>
/// Represents the action of creating a new file.
/// </summary>
public class NewFileAt : IUndoableFileOperation
{
    private readonly IFileIOHandler _fileIOHandler;
    private readonly string _path;
    private readonly string _fileName;

    public NewFileAt(IFileIOHandler fileHandler, string path, string fileName)
    {
        _fileIOHandler = fileHandler;
        _path = path;
        _fileName = fileName;
    }

    public bool Redo(out string? errorMessage) =>
        _fileIOHandler.NewFileAt(_path, _fileName, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileIOHandler.DeleteFile(Path.Combine(_path, _fileName), out errorMessage);
}

/// <summary>
/// Represents the action of creating a new directory.
/// </summary>
public class NewDirAt : IUndoableFileOperation
{
    private readonly IFileIOHandler _fileIOHandler;
    private readonly string _path;
    private readonly string _dirName;

    public NewDirAt(IFileIOHandler fileHandler, string path, string dirName)
    {
        _fileIOHandler = fileHandler;
        _path = path;
        _dirName = dirName;
    }

    public bool Redo(out string? errorMessage) =>
        _fileIOHandler.NewDirAt(_path, _dirName, out errorMessage);

    public bool Undo(out string? errorMessage) =>
        _fileIOHandler.DeleteDir(Path.Combine(_path, _dirName), out errorMessage);
}
