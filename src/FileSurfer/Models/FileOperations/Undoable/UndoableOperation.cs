namespace FileSurfer.Models.FileOperations.Undoable;

public abstract class UndoableOperation : IUndoableFileOperation
{
    protected readonly IFileIOHandler _fileIOHandler;
    protected readonly IFileSystemEntry[] _entries;

    protected UndoableOperation(IFileIOHandler fileIOHandler, IFileSystemEntry[] entries)
    {
        _fileIOHandler = fileIOHandler;
        _entries = entries;
    }

    /// <inheritdoc/>
    public IFileOperationResult Redo()
    {
        FileOperationResult result = FileOperationResult.Ok();
        for (int i = 0; i < _entries.Length; i++)
            result.AddResult(RedoAction(_entries[i], i));

        return result;
    }

    /// <inheritdoc/>
    public IFileOperationResult Undo()
    {
        FileOperationResult result = FileOperationResult.Ok();
        for (int i = 0; i < _entries.Length; i++)
            result.AddResult(UndoAction(_entries[i], i));

        return result;
    }

    protected abstract IFileOperationResult RedoAction(IFileSystemEntry entry, int index);

    protected abstract IFileOperationResult UndoAction(IFileSystemEntry entry, int index);
}
