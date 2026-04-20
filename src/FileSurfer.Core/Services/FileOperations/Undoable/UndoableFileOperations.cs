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
public sealed class MoveFilesToTrash : UndoableFileOperation
{
    private readonly IBinInteraction _binInteraction;

    protected override string InvokeVerb => "Trashing";
    protected override string UndoVerb => "Restoring";

    /// <summary>
    /// Initializes an undoable operation that moves entries to trash and restores them on undo.
    /// </summary>
    /// <param name="binInteraction">
    /// Trash integration used to move and restore files and directories.
    /// </param>
    /// <param name="fileIoHandler">
    /// File I/O handler used by the operation base type.
    /// </param>
    /// <param name="entries">
    /// Entries targeted by the operation.
    /// </param>
    public MoveFilesToTrash(
        IBinInteraction binInteraction,
        IFileIoHandler fileIoHandler,
        IFileSystemEntry[] entries
    )
        : base(fileIoHandler, entries) => _binInteraction = binInteraction;

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _binInteraction.MoveDirToTrash(entry.PathToEntry)
            : _binInteraction.MoveFileToTrash(entry.PathToEntry);

    protected override IResult UndoAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? _binInteraction.RestoreDir(entry.PathToEntry)
            : _binInteraction.RestoreFile(entry.PathToEntry);
}

/// <summary>
/// Represents the action of moving files and directories to a specific directory.
/// </summary>
public sealed class MoveFilesTo : UndoableFileOperation
{
    private readonly IPathTools _pathTools;
    private readonly string _destinationDir;

    protected override string InvokeVerb => "Moving";
    protected override string UndoVerb => "Moving back";

    /// <summary>
    /// Initializes an undoable move operation for a batch of entries.
    /// </summary>
    /// <param name="pathTools">
    /// Path helper used to compose source and destination paths.
    /// </param>
    /// <param name="fileIoHandler">
    /// File I/O handler used to execute move operations.
    /// </param>
    /// <param name="entries">
    /// Entries to move.
    /// </param>
    /// <param name="destinationDir">
    /// Destination directory path.
    /// </param>
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
public sealed class CopyFilesTo : UndoableFileOperation
{
    private readonly IPathTools _pathTools;
    private readonly string _destinationDir;

    protected override string InvokeVerb => "Copying";
    protected override string UndoVerb => "Deleting";

    /// <summary>
    /// Initializes an undoable copy operation for a batch of entries.
    /// </summary>
    /// <param name="pathTools">
    /// Path helper used to compose copied entry paths.
    /// </param>
    /// <param name="fileIoHandler">
    /// File I/O handler used to execute copy and undo-delete operations.
    /// </param>
    /// <param name="entries">
    /// Entries to copy.
    /// </param>
    /// <param name="destinationDir">
    /// Destination directory path.
    /// </param>
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
public sealed class DuplicateFiles : UndoableFileOperation
{
    private readonly IPathTools _pathTools;
    private readonly string[] _copyNames;
    private readonly string _parentDir;

    protected override string InvokeVerb => "Duplicating";
    protected override string UndoVerb => "Deleting";

    /// <summary>
    /// Initializes an undoable duplicate operation for a batch of entries.
    /// </summary>
    /// <param name="pathTools">
    /// Path helper used to compose duplicate output paths.
    /// </param>
    /// <param name="fileIoHandler">
    /// File I/O handler used for duplication and undo-delete operations.
    /// </param>
    /// <param name="entries">
    /// Entries to duplicate.
    /// </param>
    /// <param name="copyNames">
    /// Target names for duplicates, aligned by index with <paramref name="entries"/>.
    /// </param>
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
public sealed class RenameMultiple : UndoableFileOperation
{
    private readonly IPathTools _pathTools;
    private readonly string[] _newNames;
    private readonly string _dirName;

    protected override string InvokeVerb => "Renaming";
    protected override string UndoVerb => "Renaming";

    /// <summary>
    /// Initializes an undoable multi-entry rename operation.
    /// </summary>
    /// <param name="pathTools">
    /// Path helper used to compose renamed entry paths.
    /// </param>
    /// <param name="fileIoHandler">
    /// File I/O handler used to execute rename operations.
    /// </param>
    /// <param name="entries">
    /// Entries to rename.
    /// </param>
    /// <param name="newNames">
    /// Target names aligned by index with <paramref name="entries"/>.
    /// </param>
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

    /// <summary>
    /// Initializes an undoable operation that moves a directory's contents into its parent directory.
    /// </summary>
    /// <param name="fileIoHandler">
    /// File I/O handler used to move, create, rename, and delete entries.
    /// </param>
    /// <param name="infoProvider">
    /// File information provider used to enumerate directory contents and check path existence.
    /// </param>
    /// <param name="dirPath">
    /// Path of the directory to flatten.
    /// </param>
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

    /// <summary>
    /// Initializes an undoable rename operation for a single file-system entry.
    /// </summary>
    /// <param name="pathTools">
    /// Path helper used to compose the renamed entry path.
    /// </param>
    /// <param name="fileHandler">
    /// File I/O handler used to execute rename operations.
    /// </param>
    /// <param name="entry">
    /// Entry to rename.
    /// </param>
    /// <param name="newName">
    /// Target name for <paramref name="entry"/>.
    /// </param>
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

    /// <summary>
    /// Initializes an undoable operation that creates a file and deletes it on undo.
    /// </summary>
    /// <param name="pathTools">
    /// Path helper used to compose the created file path.
    /// </param>
    /// <param name="fileHandler">
    /// File I/O handler used to create and delete the file.
    /// </param>
    /// <param name="path">
    /// Directory where the file should be created.
    /// </param>
    /// <param name="fileName">
    /// Name of the file to create.
    /// </param>
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

    /// <summary>
    /// Initializes an undoable operation that creates a directory and deletes it on undo.
    /// </summary>
    /// <param name="pathTools">
    /// Path helper used to compose the created directory path.
    /// </param>
    /// <param name="fileHandler">
    /// File I/O handler used to create and delete the directory.
    /// </param>
    /// <param name="path">
    /// Parent directory where the new directory should be created.
    /// </param>
    /// <param name="dirName">
    /// Name of the directory to create.
    /// </param>
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
        _fileIoHandler.DeleteDir(_dirPath).ToTask();
}
