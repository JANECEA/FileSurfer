using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.Shell;

namespace FileSurfer.Core.Services.FileOperations.Undoable;

/// <summary>
/// Represents the action of moving selected files to the system trash.
/// </summary>
public sealed class MoveFilesToTrash : UndoableOperation
{
    private readonly IBinInteraction _fileRestorer;

    protected override string InvokeOpName => "Trashing";
    protected override string UndoOpName => "Restoring";

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

    protected override string InvokeOpName => "Moving";
    protected override string UndoOpName => "Moving back";

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
    }

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIoHandler.MoveDirTo(entry.PathToEntry, _destinationDir)
            : FileIoHandler.MoveFileTo(entry.PathToEntry, _destinationDir);

    protected override IResult UndoAction(IFileSystemEntry entry, int index)
    {
        string newPath = _pathTools.Combine(_destinationDir, entry.Name);
        string oldParentDir = _pathTools.GetParentDir(entry.PathToEntry);
        return entry is DirectoryEntry
            ? FileIoHandler.MoveDirTo(newPath, oldParentDir)
            : FileIoHandler.MoveFileTo(newPath, oldParentDir);
    }
}

/// <summary>
/// Represents the action of copying files and directories to a specific directory.
/// </summary>
public sealed class CopyFilesTo : UndoableOperation
{
    private readonly IPathTools _pathTools;
    private readonly string _destinationDir;

    protected override string InvokeOpName => "Copying";
    protected override string UndoOpName => "Deleting";

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

    protected override string InvokeOpName => "Duplicating";
    protected override string UndoOpName => "Deleting";

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

    protected override string InvokeOpName => "Renaming";
    protected override string UndoOpName => "Renaming";

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
    private readonly IFileInfoProvider _infoProvider;
    private readonly IPathTools _pathTools;
    private readonly string _dirPath;
    private readonly string _parentDir;
    private readonly string _dirName;

    private IFileSystemEntry[] _movedEntries = [];

    public FlattenFolder(
        IFileIoHandler fileIoHandler,
        IFileInfoProvider infoProvider,
        string dirPath
    )
    {
        _fileIoHandler = fileIoHandler;
        _infoProvider = infoProvider;
        _pathTools = infoProvider.PathTools;
        _dirPath = dirPath;
        _dirName = _pathTools.GetFileName(dirPath);

        _parentDir = _pathTools.GetParentDir(dirPath);
    }

    public async Task<IResult> InvokeAsync(ProgressReporter reporter, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_parentDir))
            return SimpleResult.Error($"Cannot flatten top level directory: \"{_dirPath}\"");

        IResult result = RenameIfConflict(out string newDirPath);
        if (!result.IsOk)
            return result;

        var entriesR = await _infoProvider.GetPathEntriesAsync(newDirPath, true, true, ct);
        if (!entriesR.IsOk)
            return entriesR;

        IFileSystemEntry[] oldEntries = entriesR
            .Value.Dirs.Cast<IFileSystemEntry>()
            .Concat(entriesR.Value.Files)
            .ToArray();

        result = await new MoveFilesTo(
            _infoProvider.PathTools,
            _fileIoHandler,
            oldEntries,
            _parentDir
        ).InvokeAsync(reporter, ct);
        _movedEntries = GetNewEntries(oldEntries);

        return result.IsOk ? _fileIoHandler.DeleteDir(newDirPath) : result;
    }

    private IResult RenameIfConflict(out string newDirPath)
    {
        newDirPath = _dirPath;
        if (!_infoProvider.Exists(_pathTools.Combine(_dirPath, _dirName)).AsPath)
            return SimpleResult.Ok();

        string newName = FileNameGenerator.GetNameMultipleDirs(
            _infoProvider,
            _dirName,
            _parentDir,
            _dirPath
        );
        newDirPath = _pathTools.Combine(_parentDir, newName);
        return _fileIoHandler.RenameDirAt(_dirPath, newName);
    }

    private IFileSystemEntry[] GetNewEntries(IFileSystemEntry[] oldEntries)
    {
        var dirs = oldEntries
            .OfType<DirectoryEntry>()
            .Select(d => new DirectoryEntry(_pathTools.Combine(_parentDir, d.Name), _pathTools));
        var files = oldEntries
            .OfType<FileEntry>()
            .Select(f => new FileEntry(_pathTools.Combine(_parentDir, f.Name), _pathTools));

        return dirs.Cast<IFileSystemEntry>().Concat(files).ToArray();
    }

    public async Task<IResult> UndoAsync(ProgressReporter reporter, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_parentDir))
            return SimpleResult.Error("This operation is invalid.");

        IResult result = CheckConditions(out string newDirName);
        if (!result.IsOk)
            return result;

        string newDirPath = _pathTools.Combine(_parentDir, newDirName);
        result = _fileIoHandler.NewDirAt(_parentDir, newDirName);
        if (!result.IsOk)
            return result;

        result = await new MoveFilesTo(
            _infoProvider.PathTools,
            _fileIoHandler,
            _movedEntries,
            newDirPath
        ).InvokeAsync(reporter, ct);

        if (!result.IsOk)
            return result;

        return _pathTools.NamesAreEqual(_dirName, newDirName)
            ? SimpleResult.Ok()
            : _fileIoHandler.RenameDirAt(newDirPath, _dirName);
    }

    private SimpleResult CheckConditions(out string newDirName)
    {
        newDirName = _dirName;
        if (!_infoProvider.Exists(_dirPath).AsPath)
            return SimpleResult.Ok();

        if (!_movedEntries.Any(e => _pathTools.NamesAreEqual(_dirName, e.Name)))
            return SimpleResult.Error($"Path: \"{_dirPath}\" already exists.");

        newDirName = FileNameGenerator.GetAvailableName(_infoProvider, _parentDir, _dirName);
        return SimpleResult.Ok();
    }
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

    public Task<IResult> InvokeAsync(ProgressReporter reporter, CancellationToken ct) =>
        _entry is DirectoryEntry
            ? _fileIoHandler.RenameDirAt(_entry.PathToEntry, _newName).ToTask()
            : _fileIoHandler.RenameFileAt(_entry.PathToEntry, _newName).ToTask();

    public Task<IResult> UndoAsync(ProgressReporter reporter, CancellationToken ct) =>
        _entry is DirectoryEntry
            ? _fileIoHandler.RenameDirAt(_newPath, _entry.Name).ToTask()
            : _fileIoHandler.RenameFileAt(_newPath, _entry.Name).ToTask();
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

    public Task<IResult> InvokeAsync(ProgressReporter reporter, CancellationToken ct) =>
        _fileIoHandler.NewFileAt(_path, _fileName).ToTask();

    public Task<IResult> UndoAsync(ProgressReporter reporter, CancellationToken ct) =>
        _fileIoHandler.DeleteFile(_filePath).ToTask();
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

    public Task<IResult> InvokeAsync(ProgressReporter reporter, CancellationToken ct) =>
        _fileIoHandler.NewDirAt(_path, _dirName).ToTask();

    public Task<IResult> UndoAsync(ProgressReporter reporter, CancellationToken ct) =>
        _fileIoHandler.DeleteFile(_dirPath).ToTask();
}
