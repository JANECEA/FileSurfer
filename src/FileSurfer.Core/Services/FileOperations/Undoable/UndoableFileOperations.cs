using System;
using System.Collections.Generic;
using System.IO;
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
        : base(fileIoHandler, entries)
    {
        _fileRestorer = fileRestorer;
    }

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
    private readonly string _destinationDir;
    private readonly string _originalDir;

    public MoveFilesTo(
        IFileIoHandler fileIoHandler,
        IFileSystemEntry[] entries,
        string destinationDir
    )
        : base(fileIoHandler, entries)
    {
        _destinationDir = destinationDir;
        _originalDir =
            Path.GetDirectoryName(entries[0].PathToEntry) ?? throw new InvalidOperationException();
    }

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIoHandler.MoveDirTo(entry.PathToEntry, _destinationDir)
            : FileIoHandler.MoveFileTo(entry.PathToEntry, _destinationDir);

    protected override IResult UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = Path.Combine(_destinationDir, entry.Name);
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
    private readonly string _destinationDir;

    public CopyFilesTo(
        IFileIoHandler fileIoHandler,
        IFileSystemEntry[] entries,
        string destinationDir
    )
        : base(fileIoHandler, entries) => _destinationDir = destinationDir;

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIoHandler.CopyDirTo(entry.PathToEntry, _destinationDir)
            : FileIoHandler.CopyFileTo(entry.PathToEntry, _destinationDir);

    protected override IResult UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = Path.Combine(_destinationDir, entry.Name);
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
    private readonly string[] _copyNames;
    private readonly string _parentDir;

    public DuplicateFiles(
        IFileIoHandler fileIoHandler,
        IFileSystemEntry[] entries,
        string[] copyNames
    )
        : base(fileIoHandler, entries)
    {
        _copyNames = copyNames;
        _parentDir =
            Path.GetDirectoryName(entries[0].PathToEntry) ?? throw new InvalidOperationException();
    }

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIoHandler.DuplicateDir(entry.PathToEntry, _copyNames[index])
            : FileIoHandler.DuplicateFile(entry.PathToEntry, _copyNames[index]);

    protected override IResult UndoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIoHandler.DeleteDir(Path.Combine(_parentDir, _copyNames[index]))
            : FileIoHandler.DeleteFile(Path.Combine(_parentDir, _copyNames[index]));
}

/// <summary>
/// Represents the action of renaming multiple files or directories.
/// </summary>
public sealed class RenameMultiple : UndoableOperation
{
    private readonly string[] _newNames;
    private readonly string _dirName;

    public RenameMultiple(
        IFileIoHandler fileIoHandler,
        IFileSystemEntry[] entries,
        string[] newNames
    )
        : base(fileIoHandler, entries)
    {
        _newNames = newNames;
        _dirName =
            Path.GetDirectoryName(entries[0].PathToEntry) ?? throw new InvalidOperationException();
    }

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIoHandler.RenameDirAt(entry.PathToEntry, _newNames[index])
            : FileIoHandler.RenameFileAt(entry.PathToEntry, _newNames[index]);

    protected override IResult UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = Path.Combine(_dirName, _newNames[index]);
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
        _dirPath = dirPath;
        _dirName = Path.GetFileName(dirPath);
        _parentDir = Path.GetDirectoryName(dirPath);
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
        if (!_fileInfoProvider.PathExists(Path.Combine(_dirPath, _dirName)))
            return SimpleResult.Ok();

        string newName = FileNameGenerator.GetNameMultipleDirs(
            _fileInfoProvider,
            _dirName,
            _parentDir!,
            _dirPath
        );
        newDirPath = Path.Combine(_parentDir!, newName);
        return _fileIoHandler.RenameDirAt(_dirPath, newName);
    }

    public IResult Undo()
    {
        Result result = Result.Ok();
        if (!result.MergeResult(CheckConditions(out string newDirName)).IsOk)
            return result;

        string newDirPath = Path.Combine(_parentDir!, newDirName);

        if (!result.MergeResult(_fileIoHandler.NewDirAt(_parentDir!, newDirName)).IsOk)
            return result;

        foreach (DirectoryEntryInfo dir in _containedDirs)
        {
            string dirPath = Path.Combine(_parentDir!, Path.GetFileName(dir.PathToEntry));
            result.MergeResult(_fileIoHandler.MoveDirTo(dirPath, newDirPath));
        }
        foreach (FileEntryInfo file in _containedFiles)
        {
            string filePath = Path.Combine(_parentDir!, Path.GetFileName(file.PathToEntry));
            result.MergeResult(_fileIoHandler.MoveFileTo(filePath, newDirPath));
        }

        if (!result.IsOk)
            return result;

        return string.Equals(_dirName, newDirName, StringComparison.OrdinalIgnoreCase)
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

    private static bool ContainsSameName(string name, IEnumerable<IFileSystemEntry> entries) =>
        entries.Any(entry => LocalPathTools.NamesAreEqual(name, entry.Name)); // TODO make polymorphic
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

    public RenameOne(IFileIoHandler fileHandler, IFileSystemEntry entry, string newName)
    {
        _fileIoHandler = fileHandler;
        _entry = entry;
        _newName = newName;
        string dirName =
            Path.GetDirectoryName(entry.PathToEntry) ?? throw new InvalidOperationException();
        _newPath = Path.Combine(dirName, newName);
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

    public NewFileAt(IFileIoHandler fileHandler, string path, string fileName)
    {
        _fileIoHandler = fileHandler;
        _path = path;
        _fileName = fileName;
    }

    public IResult Invoke() => _fileIoHandler.NewFileAt(_path, _fileName);

    public IResult Undo() => _fileIoHandler.DeleteFile(Path.Combine(_path, _fileName));
}

/// <summary>
/// Represents the action of creating a new directory.
/// </summary>
public sealed class NewDirAt : IUndoableFileOperation
{
    private readonly IFileIoHandler _fileIoHandler;
    private readonly string _path;
    private readonly string _dirName;

    public NewDirAt(IFileIoHandler fileHandler, string path, string dirName)
    {
        _fileIoHandler = fileHandler;
        _path = path;
        _dirName = dirName;
    }

    public IResult Invoke() => _fileIoHandler.NewDirAt(_path, _dirName);

    public IResult Undo() => _fileIoHandler.DeleteDir(Path.Combine(_path, _dirName));
}
