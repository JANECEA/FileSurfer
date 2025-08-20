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

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIOHandler.MoveDirToTrash(entry.PathToEntry)
            : FileIOHandler.MoveFileToTrash(entry.PathToEntry);

    protected override IResult UndoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIOHandler.RestoreDir(entry.PathToEntry)
            : FileIOHandler.RestoreFile(entry.PathToEntry);
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

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIOHandler.MoveDirTo(entry.PathToEntry, _destinationDir)
            : FileIOHandler.MoveFileTo(entry.PathToEntry, _destinationDir);

    protected override IResult UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = Path.Combine(_destinationDir, entry.Name);
        return entry is DirectoryEntry
            ? FileIOHandler.MoveDirTo(newPath, _originalDir)
            : FileIOHandler.MoveFileTo(newPath, _originalDir);
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

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIOHandler.CopyDirTo(entry.PathToEntry, _destinationDir)
            : FileIOHandler.CopyFileTo(entry.PathToEntry, _destinationDir);

    protected override IResult UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = Path.Combine(_destinationDir, entry.Name);
        return entry is DirectoryEntry
            ? FileIOHandler.DeleteDir(newPath)
            : FileIOHandler.DeleteFile(newPath);
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

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIOHandler.DuplicateDir(entry.PathToEntry, _copyNames[index])
            : FileIOHandler.DuplicateFile(entry.PathToEntry, _copyNames[index]);

    protected override IResult UndoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIOHandler.DeleteDir(Path.Combine(_parentDir, _copyNames[index]))
            : FileIOHandler.DeleteFile(Path.Combine(_parentDir, _copyNames[index]));
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

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIOHandler.RenameDirAt(entry.PathToEntry, _newNames[index])
            : FileIOHandler.RenameFileAt(entry.PathToEntry, _newNames[index]);

    protected override IResult UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = Path.Combine(_dirName, _newNames[index]);
        return entry is DirectoryEntry
            ? FileIOHandler.RenameDirAt(newPath, Entries[index].Name)
            : FileIOHandler.RenameFileAt(newPath, Entries[index].Name);
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

    public IResult Invoke()
    {
        if (_parentDir is null)
            return SimpleResult.Error($"Cannot flatten top level directory: \"{_dirPath}\"");

        _containedDirs = _fileInfoProvider.GetPathDirs(_dirPath, _showHidden, _showProtected);
        _containedFiles = _fileInfoProvider.GetPathFiles(_dirPath, _showHidden, _showProtected);

        Result result = Result.Ok();
        foreach (string containedDir in _containedDirs)
            result.MergeResult(_fileIOHandler.MoveDirTo(containedDir, _parentDir));

        foreach (string containedFile in _containedFiles)
            result.MergeResult(_fileIOHandler.MoveFileTo(containedFile, _parentDir));

        return result.IsOk ? _fileIOHandler.DeleteDir(_dirPath) : result;
    }

    public IResult Undo()
    {
        if (_parentDir is null)
            return SimpleResult.Error($"Cannot create a top level directory: \"{_dirPath}\"");

        IResult newDirResult = _fileIOHandler.NewDirAt(_parentDir, Path.GetFileName(_dirPath));
        if (!newDirResult.IsOk)
            return newDirResult;

        Result result = Result.Ok();
        foreach (string containedDir in _containedDirs)
        {
            string dirPath = Path.Combine(_parentDir, Path.GetFileName(containedDir));
            result.MergeResult(_fileIOHandler.MoveDirTo(dirPath, _dirPath));
        }
        foreach (string containedFile in _containedFiles)
        {
            string filePath = Path.Combine(_parentDir, Path.GetFileName(containedFile));
            result.MergeResult(_fileIOHandler.MoveFileTo(filePath, _dirPath));
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

    public IResult Invoke() =>
        _entry is DirectoryEntry
            ? _fileIOHandler.RenameDirAt(_entry.PathToEntry, _newName)
            : _fileIOHandler.RenameFileAt(_entry.PathToEntry, _newName);

    public IResult Undo() =>
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

    public IResult Invoke() => _fileIOHandler.NewFileAt(_path, _fileName);

    public IResult Undo() => _fileIOHandler.DeleteFile(Path.Combine(_path, _fileName));
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

    public IResult Invoke() => _fileIOHandler.NewDirAt(_path, _dirName);

    public IResult Undo() => _fileIOHandler.DeleteDir(Path.Combine(_path, _dirName));
}
