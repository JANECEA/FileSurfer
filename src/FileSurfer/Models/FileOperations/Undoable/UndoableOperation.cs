namespace FileSurfer.Models.FileOperations.Undoable;

public abstract class UndoableOperation : IUndoableFileOperation
{
    protected readonly IFileIOHandler FileIOHandler;
    protected readonly IFileSystemEntry[] Entries;

    protected UndoableOperation(IFileIOHandler fileIOHandler, IFileSystemEntry[] entries)
    {
        FileIOHandler = fileIOHandler;
        Entries = entries;
    }

    public IResult Invoke()
    {
        Result result = Result.Ok();
        for (int i = 0; i < Entries.Length; i++)
            result.MergeResult(InvokeAction(Entries[i], i));

        return result;
    }

    public IResult Undo()
    {
        Result result = Result.Ok();
        for (int i = 0; i < Entries.Length; i++)
            result.MergeResult(UndoAction(Entries[i], i));

        return result;
    }

    /// <summary>
    /// Represents a invoke-action invoked on <see cref="IFileSystemEntry"/> from <see cref="Entries"/>.
    /// </summary>
    /// <param name="entry"><see cref="IFileSystemEntry"/> for the invoke-action.</param>
    /// <param name="index">Entry's index in <see cref="Entries"/>, in case it is useful.</param>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    protected abstract IResult InvokeAction(IFileSystemEntry entry, int index);

    /// <summary>
    /// Represents a undo-action invoked on <see cref="IFileSystemEntry"/> from <see cref="Entries"/>.
    /// </summary>
    /// <param name="entry"><see cref="IFileSystemEntry"/> for the undo-action.</param>
    /// <param name="index">Entry's index in <see cref="Entries"/>, in case it is useful.</param>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    protected abstract IResult UndoAction(IFileSystemEntry entry, int index);
}
