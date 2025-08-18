namespace FileSurfer.Models.FileOperations.Undoable;

public abstract class UndoableOperation : IUndoableFileOperation
{
    protected readonly IFileIOHandler _fileIOHandler;
    protected readonly IFileSystemEntry[] _entries;

    protected abstract string RedoErrorStart { get; }
    protected abstract string UndoErrorStart { get; }

    protected UndoableOperation(IFileIOHandler fileIOHandler, IFileSystemEntry[] entries)
    {
        _fileIOHandler = fileIOHandler;
        _entries = entries;
    }

    /// <inheritdoc/>
    public bool Redo(out string? errorMessage)
    {
        bool errorOccured = false;
        errorMessage = RedoErrorStart;
        for (int i = 0; i < _entries.Length; i++)
        {
            bool result = RedoAction(_entries[i], i);

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
        errorMessage = UndoErrorStart;
        for (int i = 0; i < _entries.Length; i++)
        {
            bool result = UndoAction(_entries[i], i);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{_entries[i].PathToEntry}\",";
        }
        errorMessage = errorOccured ? errorMessage.TrimEnd(',') : null;
        return !errorOccured;
    }

    protected abstract bool RedoAction(IFileSystemEntry entry, int index);

    protected abstract bool UndoAction(IFileSystemEntry entry, int index);
}
