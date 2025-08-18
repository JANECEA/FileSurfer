namespace FileSurfer.Models.FileOperations.Undoable;

/// <summary>
/// Represents an undoable file operation in the context of the <see cref="FileSurfer"/> app.
/// </summary>
public interface IUndoableFileOperation
{
    /// <summary>
    /// Undoes the file operation.
    /// Implementations of this method should reverse the effects of the original operation.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult Undo();

    /// <summary>
    /// Redoes the file operation.
    /// Implementations of this method should reapply the effects of the original operation.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult Redo();
}
