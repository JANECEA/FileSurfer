using System;
using System.IO;
using FileSurfer.Models.FileInformation;

namespace FileSurfer.Models.FileOperations.Undoable;

/// <summary>
/// Represents the action of moving selected files to the system trash.
/// </summary>
public class MoveFilesToTrash : IUndoableFileOperation
{
    private readonly IFileIOHandler _fileIOHandler;
    private readonly IFileSystemEntry[] _entries;

    public MoveFilesToTrash(IFileIOHandler fileHandler, IFileSystemEntry[] entries)
    {
        _fileIOHandler = fileHandler;
        _entries = entries;
    }

    /// <inheritdoc/>
    public bool Redo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occurred moving these files to trash:";
        foreach (IFileSystemEntry entry in _entries)
        {
            bool result =
                entry is DirectoryEntry
                    ? _fileIOHandler.MoveDirToTrash(entry.PathToEntry, out _)
                    : _fileIOHandler.MoveFileToTrash(entry.PathToEntry, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{entry.PathToEntry}\",";
        }
        errorMessage = errorOccured ? errorMessage.TrimEnd(',') : null;
        return !errorOccured;
    }

    /// <inheritdoc/>
    public bool Undo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occurred restoring these files:";
        foreach (IFileSystemEntry entry in _entries)
        {
            bool result =
                entry is DirectoryEntry
                    ? _fileIOHandler.RestoreDir(entry.PathToEntry, out _)
                    : _fileIOHandler.RestoreFile(entry.PathToEntry, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{entry.PathToEntry}\",";
        }
        errorMessage = errorOccured ? errorMessage.TrimEnd(',') : null;
        return !errorOccured;
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
/// Represents the action of moving files and directories to a specific directory.
/// </summary>
public class MoveFilesTo : IUndoableFileOperation
{
    private readonly IFileIOHandler _fileIOHandler;
    private readonly IFileSystemEntry[] _entries;
    private readonly string _destinationDir;
    private readonly string _originalDir;

    public MoveFilesTo(
        IFileIOHandler fileOpsHandler,
        IFileSystemEntry[] entries,
        string destinationDir
    )
    {
        _entries = entries;
        _fileIOHandler = fileOpsHandler;
        _destinationDir = destinationDir;
        _originalDir =
            Path.GetDirectoryName(entries[0].PathToEntry) ?? throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool Redo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured moving these files:";
        foreach (IFileSystemEntry entry in _entries)
        {
            bool result =
                entry is DirectoryEntry
                    ? _fileIOHandler.MoveDirTo(entry.PathToEntry, _destinationDir, out _)
                    : _fileIOHandler.MoveFileTo(entry.PathToEntry, _destinationDir, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{entry.PathToEntry}\",";
        }
        errorMessage = errorOccured ? errorMessage.TrimEnd(',') : null;
        return !errorOccured;
    }

    /// <inheritdoc/>
    public bool Undo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured moving these files:";
        foreach (IFileSystemEntry entry in _entries)
        {
            string newPath = Path.Combine(_destinationDir, entry.Name);
            bool result =
                entry is DirectoryEntry
                    ? _fileIOHandler.MoveDirTo(newPath, _originalDir, out _)
                    : _fileIOHandler.MoveFileTo(newPath, _originalDir, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{entry.PathToEntry}\",";
        }
        errorMessage = errorOccured ? errorMessage.TrimEnd(',') : null;
        return !errorOccured;
    }
}

/// <summary>
/// Represents the action of copying files and directories to a specific directory.
/// </summary>
public class CopyFilesTo : IUndoableFileOperation
{
    private readonly IFileIOHandler _fileIOHandler;
    private readonly IFileSystemEntry[] _entries;
    private readonly string _destinationDir;

    public CopyFilesTo(
        IFileIOHandler fileHandler,
        IFileSystemEntry[] entries,
        string destinationDir
    )
    {
        _fileIOHandler = fileHandler;
        _entries = entries;
        _destinationDir = destinationDir;
    }

    /// <inheritdoc/>
    public bool Redo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured copying these files:";
        foreach (IFileSystemEntry entry in _entries)
        {
            bool result =
                entry is DirectoryEntry
                    ? _fileIOHandler.CopyDirTo(entry.PathToEntry, _destinationDir, out _)
                    : _fileIOHandler.CopyFileTo(entry.PathToEntry, _destinationDir, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{entry.PathToEntry}\",";
        }
        errorMessage = errorOccured ? errorMessage.TrimEnd(',') : null;
        return !errorOccured;
    }

    /// <inheritdoc/>
    public bool Undo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured deleting these files:";
        foreach (IFileSystemEntry entry in _entries)
        {
            string newPath = Path.Combine(_destinationDir, entry.Name);
            bool result =
                entry is DirectoryEntry
                    ? _fileIOHandler.DeleteDir(newPath, out _)
                    : _fileIOHandler.DeleteFile(newPath, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{entry.PathToEntry}\",";
        }
        errorMessage = errorOccured ? errorMessage.TrimEnd(',') : null;
        return !errorOccured;
    }
}

/// <summary>
/// Represents the action of duplicating files or directories in a specific directory.
/// </summary>
public class DuplicateFiles : IUndoableFileOperation
{
    private readonly IFileIOHandler _fileIOHandler;
    private readonly IFileSystemEntry[] _entries;
    private readonly string[] _copyNames;
    private readonly string _parentDir;

    public DuplicateFiles(
        IFileIOHandler fileOpsHandler,
        IFileSystemEntry[] entries,
        string[] copyNames
    )
    {
        _fileIOHandler = fileOpsHandler;
        _entries = entries;
        _copyNames = copyNames;
        _parentDir =
            Path.GetDirectoryName(entries[0].PathToEntry) ?? throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool Redo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured duplicating these files:";
        for (int i = 0; i < _entries.Length; i++)
        {
            bool result =
                _entries[i] is DirectoryEntry
                    ? _fileIOHandler.DuplicateDir(_entries[i].PathToEntry, _copyNames[i], out _)
                    : _fileIOHandler.DuplicateFile(_entries[i].PathToEntry, _copyNames[i], out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{_entries[i].PathToEntry}\",";
        }
        errorMessage = errorOccured ? errorMessage.TrimEnd(',') : null;
        return !errorOccured;
    }

    /// <inheritdoc/>
    public bool Undo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured deleting these files:";
        for (int i = 0; i < _entries.Length; i++)
        {
            bool result =
                _entries[i] is DirectoryEntry
                    ? _fileIOHandler.DeleteDir(Path.Combine(_parentDir, _copyNames[i]), out _)
                    : _fileIOHandler.DeleteFile(Path.Combine(_parentDir, _copyNames[i]), out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{_entries[i].PathToEntry}\",";
        }
        errorMessage = errorOccured ? errorMessage.TrimEnd(',') : null;
        return !errorOccured;
    }
}

/// <summary>
/// Represents the action of renaming multiple files or directories.
/// </summary>
public class RenameMultiple : IUndoableFileOperation
{
    private readonly IFileIOHandler _fileIOHandler;
    private readonly IFileSystemEntry[] _entries;
    private readonly string[] _newNames;
    private readonly string _dirName;

    public RenameMultiple(IFileIOHandler fileHandler, IFileSystemEntry[] entries, string[] newNames)
    {
        _fileIOHandler = fileHandler;
        _entries = entries;
        _newNames = newNames;
        _dirName =
            Path.GetDirectoryName(entries[0].PathToEntry) ?? throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool Redo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured renaming these files:";
        for (int i = 0; i < _entries.Length; i++)
        {
            bool result =
                _entries[i] is DirectoryEntry
                    ? _fileIOHandler.RenameDirAt(_entries[i].PathToEntry, _newNames[i], out _)
                    : _fileIOHandler.RenameFileAt(_entries[i].PathToEntry, _newNames[i], out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{_entries[i].PathToEntry}\",";
        }
        errorMessage = errorOccured ? errorMessage.TrimEnd(',') : null;
        return !errorOccured;
    }

    /// <inheritdoc/>
    public bool Undo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = "Problems occured renaming these files:";
        for (int i = 0; i < _entries.Length; i++)
        {
            string newPath = Path.Combine(_dirName, _newNames[i]);
            bool result =
                _entries[i] is DirectoryEntry
                    ? _fileIOHandler.RenameDirAt(newPath, _entries[i].Name, out _)
                    : _fileIOHandler.RenameFileAt(newPath, _entries[i].Name, out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{_entries[i].PathToEntry}\",";
        }
        errorMessage = errorOccured ? errorMessage.TrimEnd(',') : null;
        return !errorOccured;
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

    /// <inheritdoc/>
    public bool Redo(out string? errorMessage) =>
        _entry is DirectoryEntry
            ? _fileIOHandler.RenameDirAt(_entry.PathToEntry, _newName, out errorMessage)
            : _fileIOHandler.RenameFileAt(_entry.PathToEntry, _newName, out errorMessage);

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public bool Redo(out string? errorMessage) =>
        _fileIOHandler.NewFileAt(_path, _fileName, out errorMessage);

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public bool Redo(out string? errorMessage) =>
        _fileIOHandler.NewDirAt(_path, _dirName, out errorMessage);

    /// <inheritdoc/>
    public bool Undo(out string? errorMessage) =>
        _fileIOHandler.DeleteDir(Path.Combine(_path, _dirName), out errorMessage);
}
