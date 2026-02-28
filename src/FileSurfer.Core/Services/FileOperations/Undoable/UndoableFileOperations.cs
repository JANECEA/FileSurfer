using System.Collections.Generic;
using System.Linq;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.Shell;

namespace FileSurfer.Core.Services.FileOperations.Undoable;

/// <summary>
/// Represents the action of moving selected files to the system trash.
/// </summary>
public sealed class MoveFilesToTrash : UndoableOperation
{
    private readonly IBinInteraction _fileRestorer;

    public MoveFilesToTrash(
        IBinInteraction fileRestorer,
        IFileIoHandler fileIoHandler,
        IFileSystemEntry[] entries
    )
        : base(fileIoHandler, entries) => _fileRestorer = fileRestorer;

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileRestorer.MoveDirToTrash(entry.PathToEntry)
            : _fileRestorer.MoveFileToTrash(entry.PathToEntry);

    protected override IResult UndoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _fileRestorer.RestoreDir(entry.PathToEntry)
            : _fileRestorer.RestoreFile(entry.PathToEntry);
}

/// <summary>
/// Represents the action of moving files and directories to a specific directory.
/// </summary>
public sealed class MoveFilesTo : UndoableOperation
{
    private readonly IPathTools _pathTools;
    private readonly string _destinationDir;
    private readonly string _originalDir;

    public MoveFilesTo(
        IPathTools pathTools,
        IFileIoHandler fileIoHandler,
        IFileSystemEntry[] entries,
        string destinationDir
    )
        : base(fileIoHandler, entries)
    {
        _pathTools = pathTools;
        _destinationDir = destinationDir;
        _originalDir =
            entries.Length > 0 ? pathTools.GetParentDir(entries[0].PathToEntry) : string.Empty;
    }

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIoHandler.MoveDirTo(entry.PathToEntry, _destinationDir)
            : FileIoHandler.MoveFileTo(entry.PathToEntry, _destinationDir);

    protected override IResult UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = _pathTools.Combine(_destinationDir, entry.Name);
        return entry is DirectoryEntry
            ? FileIoHandler.MoveDirTo(newPath, _originalDir)
            : FileIoHandler.MoveFileTo(newPath, _originalDir);
    }
}

/// <summary>
/// Represents the action of copying files and directories to a specific directory.
/// </summary>
public sealed class CopyFilesTo : UndoableOperation
{
    private readonly IPathTools _pathTools;
    private readonly string _destinationDir;

    public CopyFilesTo(
        IPathTools pathTools,
        IFileIoHandler fileIoHandler,
        IFileSystemEntry[] entries,
        string destinationDir
    )
        : base(fileIoHandler, entries)
    {
        _pathTools = pathTools;
        _destinationDir = destinationDir;
    }

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIoHandler.CopyDirTo(entry.PathToEntry, _destinationDir)
            : FileIoHandler.CopyFileTo(entry.PathToEntry, _destinationDir);

    protected override IResult UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = _pathTools.Combine(_destinationDir, entry.Name);
        return entry is DirectoryEntry
            ? FileIoHandler.DeleteDir(newPath)
            : FileIoHandler.DeleteFile(newPath);
    }
}

/// <summary>
/// Represents the action of duplicating files or directories in a specific directory.
/// </summary>
public sealed class DuplicateFiles : UndoableOperation
{
    private readonly IPathTools _pathTools;
    private readonly string[] _copyNames;
    private readonly string _parentDir;

    public DuplicateFiles(
        IPathTools pathTools,
        IFileIoHandler fileIoHandler,
        IFileSystemEntry[] entries,
        string[] copyNames
    )
        : base(fileIoHandler, entries)
    {
        _pathTools = pathTools;
        _copyNames = copyNames;
        _parentDir =
            entries.Length > 0 ? pathTools.GetParentDir(entries[0].PathToEntry) : string.Empty;
    }

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIoHandler.DuplicateDir(entry.PathToEntry, _copyNames[index])
            : FileIoHandler.DuplicateFile(entry.PathToEntry, _copyNames[index]);

    protected override IResult UndoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIoHandler.DeleteDir(_pathTools.Combine(_parentDir, _copyNames[index]))
            : FileIoHandler.DeleteFile(_pathTools.Combine(_parentDir, _copyNames[index]));
}

/// <summary>
/// Represents the action of renaming multiple files or directories.
/// </summary>
public sealed class RenameMultiple : UndoableOperation
{
    private readonly IPathTools _pathTools;
    private readonly string[] _newNames;
    private readonly string _dirName;

    public RenameMultiple(
        IPathTools pathTools,
        IFileIoHandler fileIoHandler,
        IFileSystemEntry[] entries,
        string[] newNames
    )
        : base(fileIoHandler, entries)
    {
        _pathTools = pathTools;
        _newNames = newNames;
        _dirName = pathTools.GetParentDir(entries[0].PathToEntry);
    }

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIoHandler.RenameDirAt(entry.PathToEntry, _newNames[index])
            : FileIoHandler.RenameFileAt(entry.PathToEntry, _newNames[index]);

    protected override IResult UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = _pathTools.Combine(_dirName, _newNames[index]);
        return entry is DirectoryEntry
            ? FileIoHandler.RenameDirAt(newPath, Entries[index].Name)
            : FileIoHandler.RenameFileAt(newPath, Entries[index].Name);
    }
}

/// <summary>
/// Represents the action of flattening a directory.
/// </summary>
public sealed class FlattenFolder : IUndoableFileOperation
{
    private readonly IFileIoHandler _fileIoHandler;
    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly IPathTools _pathTools;
    private readonly string _dirPath;
    private readonly string _dirName;
    private readonly string? _parentDir;

    private IReadOnlyList<DirectoryEntryInfo> _containedDirs = [];
    private IReadOnlyList<FileEntryInfo> _containedFiles = [];

    public FlattenFolder(
        IFileIoHandler fileIoHandler,
        IFileInfoProvider fileInfoProvider,
        string dirPath
    )
    {
        _fileIoHandler = fileIoHandler;
        _fileInfoProvider = fileInfoProvider;
        _pathTools = fileInfoProvider.PathTools;
        _dirPath = dirPath;
        _dirName = _pathTools.GetFileName(dirPath);
        _parentDir = _pathTools.GetParentDir(dirPath);
    }

    public IResult Invoke()
    {
        if (_parentDir is null)
            return SimpleResult.Error($"Cannot flatten top level directory: \"{_dirPath}\"");

        Result result = Result.Ok();
        if (!result.MergeResult(RenameIfConflict(out string newDirPath)).IsOk)
            return result;

        var dirsResult = _fileInfoProvider.GetPathDirs(newDirPath, true, true);
        var filesResult = _fileInfoProvider.GetPathFiles(newDirPath, true, true);

        if (!dirsResult.IsOk || !filesResult.IsOk)
            return Result.Error(dirsResult.Errors);

        _containedDirs = dirsResult.Value;
        _containedFiles = filesResult.Value;

        foreach (DirectoryEntryInfo dir in _containedDirs)
            result.MergeResult(_fileIoHandler.MoveDirTo(dir.PathToEntry, _parentDir));

        foreach (FileEntryInfo file in _containedFiles)
            result.MergeResult(_fileIoHandler.MoveFileTo(file.PathToEntry, _parentDir));

        return result.IsOk ? _fileIoHandler.DeleteDir(newDirPath) : result;
    }

    private IResult RenameIfConflict(out string newDirPath)
    {
        newDirPath = _dirPath;
        if (!_fileInfoProvider.PathExists(_pathTools.Combine(_dirPath, _dirName)))
            return SimpleResult.Ok();

        string newName = FileNameGenerator.GetNameMultipleDirs(
            _fileInfoProvider,
            _dirName,
            _parentDir!,
            _dirPath
        );
        newDirPath = _pathTools.Combine(_parentDir!, newName);
        return _fileIoHandler.RenameDirAt(_dirPath, newName);
    }

    public IResult Undo()
    {
        if (_parentDir is null)
            return SimpleResult.Error("This operation is invalid.");

        Result result = Result.Ok();
        if (!result.MergeResult(CheckConditions(out string newDirName)).IsOk)
            return result;

        string newDirPath = _pathTools.Combine(_parentDir, newDirName);

        if (!result.MergeResult(_fileIoHandler.NewDirAt(_parentDir, newDirName)).IsOk)
            return result;

        foreach (DirectoryEntryInfo dir in _containedDirs)
        {
            string dirPath = _pathTools.Combine(
                _parentDir,
                _pathTools.GetFileName(dir.PathToEntry)
            );
            result.MergeResult(_fileIoHandler.MoveDirTo(dirPath, newDirPath));
        }
        foreach (FileEntryInfo file in _containedFiles)
        {
            string filePath = _pathTools.Combine(
                _parentDir,
                _pathTools.GetFileName(file.PathToEntry)
            );
            result.MergeResult(_fileIoHandler.MoveFileTo(filePath, newDirPath));
        }

        if (!result.IsOk)
            return result;

        return _pathTools.NamesAreEqual(_dirName, newDirName)
            ? SimpleResult.Ok()
            : _fileIoHandler.RenameDirAt(newDirPath, _dirName);
    }

    private SimpleResult CheckConditions(out string newDirName)
    {
        newDirName = _dirName;
        if (_parentDir is null)
            return SimpleResult.Error($"Cannot create a top level directory: \"{_dirPath}\"");

        if (!_fileInfoProvider.PathExists(_dirPath))
            return SimpleResult.Ok();

        if (
            !ContainsSameName(_dirName, _containedDirs)
            && !ContainsSameName(_dirName, _containedFiles)
        )
            return SimpleResult.Error($"Path: \"{_dirPath}\" already exists.");

        newDirName = FileNameGenerator.GetAvailableName(_fileInfoProvider, _parentDir, _dirName);
        return SimpleResult.Ok();
    }

    private bool ContainsSameName(string name, IEnumerable<IFileSystemEntry> entries) =>
        entries.Any(entry => _pathTools.NamesAreEqual(name, entry.Name));
}

/// <summary>
/// Represents the action of renaming one file or directory.
/// </summary>
public sealed class RenameOne : IUndoableFileOperation
{
    private readonly IFileIoHandler _fileIoHandler;
    private readonly IFileSystemEntry _entry;
    private readonly string _newName;
    private readonly string _newPath;

    public RenameOne(
        IPathTools pathTools,
        IFileIoHandler fileHandler,
        IFileSystemEntry entry,
        string newName
    )
    {
        _fileIoHandler = fileHandler;
        _entry = entry;
        _newName = newName;
        string dirName = pathTools.GetParentDir(entry.PathToEntry);
        _newPath = pathTools.Combine(dirName, newName);
    }

    public IResult Invoke() =>
        _entry is DirectoryEntry
            ? _fileIoHandler.RenameDirAt(_entry.PathToEntry, _newName)
            : _fileIoHandler.RenameFileAt(_entry.PathToEntry, _newName);

    public IResult Undo() =>
        _entry is DirectoryEntry
            ? _fileIoHandler.RenameDirAt(_newPath, _entry.Name)
            : _fileIoHandler.RenameFileAt(_newPath, _entry.Name);
}

/// <summary>
/// Represents the action of creating a new file.
/// </summary>
public sealed class NewFileAt : IUndoableFileOperation
{
    private readonly IFileIoHandler _fileIoHandler;
    private readonly string _path;
    private readonly string _fileName;
    private readonly string _filePath;

    public NewFileAt(IPathTools pathTools, IFileIoHandler fileHandler, string path, string fileName)
    {
        _fileIoHandler = fileHandler;
        _path = path;
        _fileName = fileName;
        _filePath = pathTools.Combine(path, fileName);
    }

    public IResult Invoke() => _fileIoHandler.NewFileAt(_path, _fileName);

    public IResult Undo() => _fileIoHandler.DeleteFile(_filePath);
}

/// <summary>
/// Represents the action of creating a new directory.
/// </summary>
public sealed class NewDirAt : IUndoableFileOperation
{
    private readonly IFileIoHandler _fileIoHandler;
    private readonly string _path;
    private readonly string _dirName;
    private readonly string _dirPath;

    public NewDirAt(IPathTools pathTools, IFileIoHandler fileHandler, string path, string dirName)
    {
        _fileIoHandler = fileHandler;
        _path = path;
        _dirName = dirName;
        _dirPath = pathTools.Combine(path, dirName);
    }

    public IResult Invoke() => _fileIoHandler.NewDirAt(_path, _dirName);

    public IResult Undo() => _fileIoHandler.DeleteDir(_dirPath);
}
