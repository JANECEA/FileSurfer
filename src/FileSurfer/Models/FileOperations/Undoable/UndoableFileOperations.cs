using System;
using System.IO;
using FileSurfer.Models.FileInformation;

namespace FileSurfer.Models.FileOperations.Undoable;

/// <summary>
/// Represents the action of moving selected files to the system trash.
/// </summary>
public class MoveFilesToTrash : UndoableOperation
{
    public MoveFilesToTrash(IFileIOHandler fileIOHandler, IFileSystemEntry[] entries)
        : base(fileIOHandler, entries) { }

    protected override IFileOperationResult RedoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.MoveDirToTrash(entry.PathToEntry)
            : _fileIOHandler.MoveFileToTrash(entry.PathToEntry);

    protected override IFileOperationResult UndoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.RestoreDir(entry.PathToEntry)
            : _fileIOHandler.RestoreFile(entry.PathToEntry);
}

/// <summary>
/// Represents the action of moving files and directories to a specific directory.
/// </summary>
public class MoveFilesTo : UndoableOperation
{
    private readonly string _destinationDir;
    private readonly string _originalDir;

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

    protected override IFileOperationResult RedoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.MoveDirTo(entry.PathToEntry, _destinationDir)
            : _fileIOHandler.MoveFileTo(entry.PathToEntry, _destinationDir);

    protected override IFileOperationResult UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = Path.Combine(_destinationDir, entry.Name);
        return entry is DirectoryEntry
            ? _fileIOHandler.MoveDirTo(newPath, _originalDir)
            : _fileIOHandler.MoveFileTo(newPath, _originalDir);
    }
}

/// <summary>
/// Represents the action of copying files and directories to a specific directory.
/// </summary>
public class CopyFilesTo : UndoableOperation
{
    private readonly string _destinationDir;

    public CopyFilesTo(
        IFileIOHandler fileIOHandler,
        IFileSystemEntry[] entries,
        string destinationDir
    )
        : base(fileIOHandler, entries) => _destinationDir = destinationDir;

    protected override IFileOperationResult RedoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.CopyDirTo(entry.PathToEntry, _destinationDir)
            : _fileIOHandler.CopyFileTo(entry.PathToEntry, _destinationDir);

    protected override IFileOperationResult UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = Path.Combine(_destinationDir, entry.Name);
        return entry is DirectoryEntry
            ? _fileIOHandler.DeleteDir(newPath)
            : _fileIOHandler.DeleteFile(newPath);
    }
}

/// <summary>
/// Represents the action of duplicating files or directories in a specific directory.
/// </summary>
public class DuplicateFiles : UndoableOperation
{
    private readonly string[] _copyNames;
    private readonly string _parentDir;

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

    protected override IFileOperationResult RedoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.DuplicateDir(entry.PathToEntry, _copyNames[index])
            : _fileIOHandler.DuplicateFile(entry.PathToEntry, _copyNames[index]);

    protected override IFileOperationResult UndoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.DeleteDir(Path.Combine(_parentDir, _copyNames[index]))
            : _fileIOHandler.DeleteFile(Path.Combine(_parentDir, _copyNames[index]));
}

/// <summary>
/// Represents the action of renaming multiple files or directories.
/// </summary>
public class RenameMultiple : UndoableOperation
{
    private readonly string[] _newNames;
    private readonly string _dirName;

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

    protected override IFileOperationResult RedoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileIOHandler.RenameDirAt(entry.PathToEntry, _newNames[index])
            : _fileIOHandler.RenameFileAt(entry.PathToEntry, _newNames[index]);

    protected override IFileOperationResult UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = Path.Combine(_dirName, _newNames[index]);
        return entry is DirectoryEntry
            ? _fileIOHandler.RenameDirAt(newPath, _entries[index].Name)
            : _fileIOHandler.RenameFileAt(newPath, _entries[index].Name);
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

    public IFileOperationResult Redo()
    {
        if (_parentDir is null)
            return FileOperationResult.Error($"Cannot flatten top level directory: \"{_dirPath}\"");

        _containedDirs = _fileInfoProvider.GetPathDirs(_dirPath, _showHidden, _showProtected);
        _containedFiles = _fileInfoProvider.GetPathFiles(_dirPath, _showHidden, _showProtected);

        FileOperationResult result = FileOperationResult.Ok();
        foreach (string containedDir in _containedDirs)
            result.AddResult(_fileIOHandler.MoveDirTo(containedDir, _parentDir));

        foreach (string containedFile in _containedFiles)
            result.AddResult(_fileIOHandler.MoveFileTo(containedFile, _parentDir));

        return result.IsOK ? _fileIOHandler.DeleteDir(_dirPath) : result;
    }

    public IFileOperationResult Undo()
    {
        if (_parentDir is null)
            return FileOperationResult.Error(
                $"Cannot create a top level directory: \"{_dirPath}\""
            );

        IFileOperationResult newDirResult = _fileIOHandler.NewDirAt(
            _parentDir,
            Path.GetFileName(_dirPath)
        );
        if (!newDirResult.IsOK)
            return newDirResult;

        FileOperationResult result = FileOperationResult.Ok();
        foreach (string containedDir in _containedDirs)
        {
            string dirPath = Path.Combine(_parentDir, Path.GetFileName(containedDir));
            result.AddResult(_fileIOHandler.MoveDirTo(dirPath, _dirPath));
        }
        foreach (string containedFile in _containedFiles)
        {
            string filePath = Path.Combine(_parentDir, Path.GetFileName(containedFile));
            result.AddResult(_fileIOHandler.MoveFileTo(filePath, _dirPath));
        }
        return result;
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

    public IFileOperationResult Redo() =>
        _entry is DirectoryEntry
            ? _fileIOHandler.RenameDirAt(_entry.PathToEntry, _newName)
            : _fileIOHandler.RenameFileAt(_entry.PathToEntry, _newName);

    public IFileOperationResult Undo() =>
        _entry is DirectoryEntry
            ? _fileIOHandler.RenameDirAt(_newPath, _entry.Name)
            : _fileIOHandler.RenameFileAt(_newPath, _entry.Name);
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

    public IFileOperationResult Redo() => _fileIOHandler.NewFileAt(_path, _fileName);

    public IFileOperationResult Undo() => _fileIOHandler.DeleteFile(Path.Combine(_path, _fileName));
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

    public IFileOperationResult Redo() => _fileIOHandler.NewDirAt(_path, _dirName);

    public IFileOperationResult Undo() => _fileIOHandler.DeleteDir(Path.Combine(_path, _dirName));
}
